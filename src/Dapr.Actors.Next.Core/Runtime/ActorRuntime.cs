// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Diagnostics;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Dispatching;
using Dapr.Actors.Next.Abstractions.Filters;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Abstractions.Scheduling;
using Dapr.Actors.Next.Abstractions.State.Versioning;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.Observability;
using Dapr.Actors.Next.Core.Registration;
using Dapr.Actors.Next.Core.Scheduling;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Actors.Next.Core.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dapr.Actors.Next.Core.Runtime;

/// <summary>
/// Default actor runtime host.
/// </summary>
public sealed class ActorRuntime(
    IServiceScopeFactory scopeFactory,
    ActorRuntimeRegistry registry,
    IActorStateStore stateStore,
    IActorWireSerializer serializer,
    IActorStateFaultInjector stateFaultInjector,
    IActorScheduler scheduler,
    IEnumerable<IActorTurnFilter> filters,
    IOptions<DaprActorsOptions> options,
    ILogger<ActorRuntime> logger,
    IActorStateMigrator? stateMigrator = null) : IActorRuntime
{
    private static readonly ActivitySource ActivitySource = new("Dapr.Actors.Next.Core");
    private readonly ConcurrentDictionary<ActorKey, ActorActivation> activations = [];
    private readonly ConcurrentDictionary<ActorKey, RuntimeActorMailbox> mailboxes = [];
    private readonly IReadOnlyList<IActorTurnFilter> _filters = filters.ToArray();

    /// <inheritdoc />
    public Task<byte[]?> InvokeAsync(string actorType, string actorId, string methodName, ReadOnlyMemory<byte> payload, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        var requestContext = ActorRequestContextSnapshot.Capture();
        return DispatchAsync(new ActorRuntimeRequest(actorType, ActorId.Create(actorId), methodName, ActorTurnKind.Invoke, payload, headers, requestContext), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<byte[]?> DispatchAsync(ActorRuntimeRequest request, CancellationToken cancellationToken = default)
    {
        request = request with { Headers = ActorHeaders.WithCurrentReentrancy(request.Headers) };
        var key = new ActorKey(request.ActorType, request.ActorId.Value);
        var current = ActorTurnExecution.Current;
        if (current is not null && current.Key.Equals(key) && IsSameCallChain(current.Headers, request.Headers))
        {
            current.ReentrantDepth++;
            try
            {
                return await ExecuteTurnAsync(request, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                current.ReentrantDepth--;
            }
        }

        var mailbox = mailboxes.GetOrAdd(key, static (actorKey, runtime) => new RuntimeActorMailbox(actorKey.ActorType, ActorId.Create(actorKey.ActorId), runtime), this);

        // Fast path: when the scheduler permits inline execution and the mailbox is idle, run the turn directly
        // on the caller. This preserves one-turn-at-a-time semantics via the mailbox execution slot while
        // avoiding the thread-pool hop, the TaskCompletionSource, and the per-call cancellation registration
        // that the queued path below incurs. Any turns enqueued while this one ran are drained afterward.
        if (scheduler.AllowsInlineExecution && mailbox.TryClaimInlineTurn())
        {
            try
            {
                return await ExecuteTurnAsync(request, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (mailbox.ReleaseInlineTurn())
                {
                    await scheduler.ScheduleAsync(mailbox, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        var work = new ActorTurnWork(request, cancellationToken);
        await mailbox.EnqueueWorkAsync(work, cancellationToken).ConfigureAwait(false);
        await scheduler.ScheduleAsync(mailbox, cancellationToken).ConfigureAwait(false);
        return await work.Task.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeactivateAsync(string actorType, ActorId actorId, CancellationToken cancellationToken = default)
    {
        var key = new ActorKey(actorType, actorId.Value);
        if (activations.TryRemove(key, out var activation))
        {
            await activation.OnDeactivateAsync(cancellationToken).ConfigureAwait(false);
            await activation.DisposeAsync().ConfigureAwait(false);
        }
    }

    internal async Task<byte[]?> ExecuteTurnAsync(ActorRuntimeRequest request, CancellationToken cancellationToken)
    {
        request = request with { Headers = ActorHeaders.EnsureReentrancy(request.Headers) };
        if (request.Kind == ActorTurnKind.Deactivate)
        {
            await DeactivateAsync(request.ActorType, request.ActorId, cancellationToken).ConfigureAwait(false);
            return null;
        }

        var registration = registry.GetByActorType(request.ActorType);
        var activation = await GetOrActivateAsync(registration, request.ActorId, cancellationToken).ConfigureAwait(false);
        var dispatchRequest = new ActorDispatchRequest(request.ActorType, request.ActorId, request.OperationName, request.Payload, request.Headers, request.RequestContext);
        var methodContext = ToMethodContext(dispatchRequest);
        Activity? activity = null;
        if (ActivitySource.HasListeners())
        {
            var parentContext = ActorRequestContextSnapshot.CreateActivityContext(request.RequestContext);
            var links = CreateLinks(request);
            activity = ActivitySource.StartActivity($"actor.{request.Kind}.{request.ActorType}.{request.OperationName}", ActivityKind.Server, parentContext, links: links);
        }

        using var activityScope = activity;
        using var restore = ActorRequestContextSnapshot.ShouldRestore(request.RequestContext)
            ? ActorRequestContextSnapshot.Restore(request.RequestContext)
            : null;

        using var executionScope = ActorTurnExecution.Push(new ActorTurnExecution(new ActorKey(request.ActorType, request.ActorId.Value), request.Headers));

        ActorDispatchResponse? response = null;
        Exception? exception = null;
        try
        {
            // Invoke, reminder, and timer callbacks are at-least-once. The unit-of-work save below is
            // intentionally completed before the transport response is sent, so a dropped stream after
            // commit may re-run the turn against already durable state. Reminder idempotency is keyed on
            // name plus due_time metadata, never on the callback correlation id.
            if (_filters.Count == 0)
            {
                await activation.OnPreActorMethodAsync(methodContext, cancellationToken).ConfigureAwait(false);
                response = await registration.Dispatcher.DispatchAsync(activation.Instance, dispatchRequest, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var turnContext = new ActorTurnContext(request.ActorType, request.ActorId, request.OperationName, request.Kind, request.Headers, request.RequestContext, cancellationToken);
                await BuildPipeline(activation, methodContext, innerCancellationToken =>
                {
                    return async () =>
                    {
                        response = await registration.Dispatcher.DispatchAsync(activation.Instance, dispatchRequest, innerCancellationToken).ConfigureAwait(false);
                    };
                })(turnContext).ConfigureAwait(false);
            }

            await activation.OnPostActorMethodAsync(methodContext, null, cancellationToken).ConfigureAwait(false);
            await activation.StateUnitOfWork.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            exception = ex;
            await activation.OnPostActorMethodAsync(methodContext, exception, cancellationToken).ConfigureAwait(false);
            logger.LogError(ex, "Actor turn failed for {ActorType}/{ActorId}/{OperationName}.", request.ActorType, request.ActorId, request.OperationName);
            throw;
        }
        finally
        {
            activity?.SetTag("dapr.actor.exception", exception?.GetType().FullName);
        }

        // Void turns produce a response with a null Result; preserve the historical empty-payload (non-null)
        // return for those, and hand back the dispatcher's raw UTF-8 result bytes for value-returning turns.
        return response is null ? null : response.Value.Result ?? Array.Empty<byte>();
    }

    private async Task<ActorActivation> GetOrActivateAsync(ActorRuntimeRegistration registration, ActorId actorId, CancellationToken cancellationToken)
    {
        var key = new ActorKey(registration.ActorType, actorId.Value);
        if (activations.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var state = new ActorStateUnitOfWork(
            registration.ActorType,
            actorId,
            stateStore,
            serializer,
            stateFaultInjector,
            stateMigrator,
            options.Value.DisableStateMigration || registration.Options?.DisableStateMigration == true);
        var activationContext = new ActorActivationContext(actorId, state);

        // The activation service provider layers the activation built-ins over a per-activation DI scope that
        // is created lazily on first use. Actors that depend only on those built-ins never allocate a scope.
        // The provider owns the scope's lifetime and is disposed with the activation, preserving disposal of
        // any scoped or transient services it resolves.
        var provider = new ActorActivationServiceProvider(scopeFactory, activationContext);
        var actor = registration.Factory(provider, actorId);
        var activation = new ActorActivation(registration.ActorType, actorId, actor, state, state, provider, registration.Lifecycle);
        if (!activations.TryAdd(key, activation))
        {
            await activation.DisposeAsync().ConfigureAwait(false);
            return activations[key];
        }

        await activation.OnActivateAsync(cancellationToken).ConfigureAwait(false);
        return activation;
    }

    private ActorTurnDelegate BuildPipeline(
        ActorActivation activation,
        ActorMethodContext methodContext,
        Func<CancellationToken, Func<Task>> invokeFactory)
    {
        ActorTurnDelegate next = async context =>
        {
            await activation.OnPreActorMethodAsync(methodContext, context.CancellationToken).ConfigureAwait(false);
            await invokeFactory(context.CancellationToken)().ConfigureAwait(false);
        };

        for (var index = _filters.Count - 1; index >= 0; index--)
        {
            var filter = _filters[index];
            var capturedNext = next;
            next = context => filter.InvokeAsync(context, capturedNext);
        }

        return next;
    }

    private static ActorMethodContext ToMethodContext(ActorDispatchRequest request) =>
        new(request.ActorType, request.ActorId, request.MethodName, Array.Empty<object?>(), request.Headers);

    private static bool IsSameCallChain(IReadOnlyDictionary<string, string> currentHeaders, IReadOnlyDictionary<string, string> nextHeaders) =>
        ActorHeaders.TryGetReentrancy(currentHeaders, out _, out var current)
        && ActorHeaders.TryGetReentrancy(nextHeaders, out _, out var next)
        && string.Equals(current, next, StringComparison.Ordinal);

    private static IEnumerable<ActivityLink>? CreateLinks(ActorRuntimeRequest request)
    {
        if (request.Kind == ActorTurnKind.Reminder
            && request.Headers.TryGetValue("dapr-actors-origin-traceparent", out var traceParent)
            && ActivityContext.TryParse(traceParent, null, out var context))
        {
            return [new ActivityLink(context)];
        }

        return null;
    }

    private readonly record struct ActorKey(string ActorType, string ActorId);
}
