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
    IActorStateMigrator? stateMigrator = null) : IActorRuntime, IAsyncDisposable, IDisposable
{
    private static readonly ActivitySource ActivitySource = new("Dapr.Actors.Next.Core");
    private readonly ConcurrentDictionary<ActorKey, ActorActivation> activations = [];
    private readonly ConcurrentDictionary<ActorKey, RuntimeActorMailbox> mailboxes = [];
    private readonly IReadOnlyList<IActorTurnFilter> _filters = filters.ToArray();
    private readonly object lifecycleLock = new();
    private readonly Dictionary<RuntimeActorMailbox, int> admissions = [];
    private bool stopping;
    private Task? disposal;
    private TaskCompletionSource lifecycleChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal event Action<string, ActorId>? ActivationEnded;

    private void SignalLifecycleChange()
    {
        lock (lifecycleLock)
        {
            if (!stopping) return;
            lifecycleChanged.TrySetResult();
            lifecycleChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>Drains admitted turns and disposes every activation and its service scope.</summary>
    public ValueTask DisposeAsync()
    {
        lock (lifecycleLock)
        {
            stopping = true;
            return new ValueTask(disposal ??= ShutdownAsync());
        }
    }

    /// <inheritdoc />
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private async Task ShutdownAsync()
    {
        // Yield before invoking user lifecycle code while holding the admission lock.
        await Task.Yield();
        while (true)
        {
            RuntimeActorMailbox[] pending;
            Task changed;
            lock (lifecycleLock)
            {
                // Caller cancellation can complete DispatchAsync before its queued turn exits.
                // Keep those mailboxes alive until actual execution and queued work finish.
                pending = mailboxes.Values
                    .Where(mailbox => admissions.ContainsKey(mailbox) || mailbox.IsExecuting || mailbox.PendingCount > 0)
                    .ToArray();
                if (pending.Length == 0) break;
                changed = lifecycleChanged.Task;
            }
            // Also drain controlled/testing schedulers that have no background pump.
            foreach (var mailbox in pending)
            {
                if (!mailbox.IsExecuting && mailbox.PendingCount > 0)
                    _ = mailbox.ExecuteNextAsync();
            }
            await changed.ConfigureAwait(false);
        }
        List<Exception> failures = [];
        foreach (var key in activations.Keys)
        {
            try { await DeactivateCoreAsync(key.ActorType, ActorId.Create(key.ActorId), CancellationToken.None).ConfigureAwait(false); }
            catch (Exception ex) { failures.Add(ex); }
        }
        mailboxes.Clear();
        if (failures.Count > 0) throw new AggregateException(failures);
    }

    internal void TryRetireMailbox(RuntimeActorMailbox mailbox)
    {
        lock (lifecycleLock)
        {
            SignalLifecycleChange();
            var key = new ActorKey(mailbox.ActorType, mailbox.ActorId.Value);
            if (!admissions.ContainsKey(mailbox) && !mailbox.IsExecuting && mailbox.PendingCount == 0
                && !activations.ContainsKey(key))
                ((ICollection<KeyValuePair<ActorKey, RuntimeActorMailbox>>)mailboxes).Remove(new(key, mailbox));
        }
    }

    /// <inheritdoc />
    public Task<byte[]?> InvokeAsync(string actorType, string actorId, string methodName, ReadOnlyMemory<byte> payload, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        var requestContext = ActorRequestContextSnapshot.Capture();
        return DispatchAsync(new ActorRuntimeRequest(actorType, ActorId.Create(actorId), methodName, ActorTurnKind.Invoke, payload, headers, requestContext), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<byte[]?> DispatchAsync(ActorRuntimeRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        request = request with
        {
            Headers = ActorHeaders.EnsureReentrancy(request.Kind == ActorTurnKind.Invoke
                ? ActorHeaders.WithCurrentReentrancy(request.Headers)
                : request.Headers),
        };
        var key = new ActorKey(request.ActorType, request.ActorId.Value);
        RuntimeActorMailbox mailbox;
        lock (lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(stopping && ActorTurnExecution.Current is null
                && !mailboxes.Values.Any(active => active.HasActiveCallChain(request.Headers)), this);
            mailbox = mailboxes.GetOrAdd(key, static (actorKey, runtime) => new RuntimeActorMailbox(actorKey.ActorType, ActorId.Create(actorKey.ActorId), runtime), this);
            admissions[mailbox] = admissions.GetValueOrDefault(mailbox) + 1;
            SignalLifecycleChange();
        }
        try
        {
            return await DispatchAdmittedAsync(request, cancellationToken, mailbox).ConfigureAwait(false);
        }
        finally
        {
            lock (lifecycleLock)
            {
                if (--admissions[mailbox] == 0) admissions.Remove(mailbox);
                TryRetireMailbox(mailbox);
            }
        }
    }

    private async Task<byte[]?> DispatchAdmittedAsync(ActorRuntimeRequest request, CancellationToken cancellationToken, RuntimeActorMailbox mailbox)
    {
        if (request.Kind == ActorTurnKind.Invoke)
        {
            var registration = registry.GetByActorType(request.ActorType);
            var configured = registration.Options ?? options.Value;
            if (mailbox.TryClaimReentrantTurn(request,
                    registration.TypeOptions?.EnableReentrancy ?? configured.EnableReentrancy ?? true,
                    registration.TypeOptions?.MaxReentrantDepth ?? configured.MaxReentrantDepth ?? 32))
            {
                try
                {
                    return await ExecuteTurnCoreAsync(request, cancellationToken, mailbox, rootTurn: false).ConfigureAwait(false);
                }
                finally
                {
                    mailbox.ReleaseReentrantTurn();
                }
            }
        }

        // Fast path: when the scheduler permits inline execution and the mailbox is idle, run the turn directly
        // on the caller. This preserves one-turn-at-a-time semantics via the mailbox execution slot while
        // avoiding the thread-pool hop, the TaskCompletionSource, and the per-call cancellation registration
        // that the queued path below incurs. Any turns enqueued while this one ran are drained afterward.
        if (scheduler.AllowsInlineExecution && mailbox.TryClaimInlineTurn(request))
        {
            try
            {
                return await ExecuteTurnAsync(request, cancellationToken, mailbox).ConfigureAwait(false);
            }
            finally
            {
                if (mailbox.ReleaseInlineTurn())
                {
                    // Queued turns must progress even when the completed inline caller cancelled.
                    await scheduler.ScheduleAsync(mailbox, CancellationToken.None).ConfigureAwait(false);
                }
            }
        }

        var work = new ActorTurnWork(request, cancellationToken);
        await mailbox.EnqueueWorkAsync(work, cancellationToken).ConfigureAwait(false);
        SignalLifecycleChange();
        // Once accepted, scheduling belongs to the mailbox, not to an individual caller.
        await scheduler.ScheduleAsync(mailbox, CancellationToken.None).ConfigureAwait(false);
        return await work.Task.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeactivateAsync(string actorType, ActorId actorId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = new ActorKey(actorType, actorId.Value);
        if (!mailboxes.TryGetValue(key, out var mailbox))
        {
            return;
        }

        if (ActorTurnExecution.Current is { } current && mailbox.TryDeferDeactivation(current.Headers))
        {
            return;
        }

        await DispatchAsync(new ActorRuntimeRequest(actorType, actorId, "Deactivate", ActorTurnKind.Deactivate,
            ReadOnlyMemory<byte>.Empty, ActorHeaders.Empty,
            new ActorRequestContext(null, null, ActorHeaders.Empty)), cancellationToken).ConfigureAwait(false);
    }

    private async Task DeactivateCoreAsync(string actorType, ActorId actorId, CancellationToken cancellationToken)
    {
        var key = new ActorKey(actorType, actorId.Value);
        if (activations.TryRemove(key, out var activation))
        {
            await DeactivateCoreAsync(activation, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task DeactivateCoreAsync(ActorActivation activation, CancellationToken cancellationToken)
    {
        try
        {
            await activation.OnDeactivateAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await DisposeActivationAsync(activation).ConfigureAwait(false);
        }
    }

    private async ValueTask DisposeActivationAsync(ActorActivation activation)
    {
        try
        {
            await activation.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            ActivationEnded?.Invoke(activation.ActorType, activation.ActorId);
        }
    }

    internal async Task<byte[]?> ExecuteTurnAsync(ActorRuntimeRequest request, CancellationToken cancellationToken, RuntimeActorMailbox mailbox)
    {
        try
        {
            return await ExecuteTurnCoreAsync(request, cancellationToken, mailbox).ConfigureAwait(false);
        }
        finally
        {
            await mailbox.CloseTurnAsync().ConfigureAwait(false);
            if (mailbox.TakeDeferredDeactivation())
            {
                await DeactivateCoreAsync(request.ActorType, request.ActorId, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private async Task<byte[]?> ExecuteTurnCoreAsync(ActorRuntimeRequest request, CancellationToken cancellationToken, RuntimeActorMailbox mailbox, bool rootTurn = true)
    {
        request = request with { Headers = ActorHeaders.EnsureReentrancy(request.Headers) };
        if (request.Kind == ActorTurnKind.Deactivate)
        {
            await DeactivateCoreAsync(request.ActorType, request.ActorId, cancellationToken).ConfigureAwait(false);
            return null;
        }

        var registration = registry.GetByActorType(request.ActorType);
        var activation = await GetOrActivateAsync(registration, request.ActorId, ActorHeaders.GetActivationId(request.Headers), cancellationToken).ConfigureAwait(false);
        mailbox.MarkActivated();
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

            if (rootTurn)
            {
                await mailbox.WaitForReentrantTurnsAsync().ConfigureAwait(false);
            }

            await activation.OnPostActorMethodAsync(methodContext, null, cancellationToken).ConfigureAwait(false);
            if (rootTurn)
            {
                await mailbox.CloseTurnAsync().ConfigureAwait(false);
            }

            await activation.StateUnitOfWork.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            exception = ex;
            if (rootTurn)
            {
                await mailbox.WaitForReentrantTurnsAsync().ConfigureAwait(false);
                // A failed flush may have closed admission, but the error lifecycle hook can invoke actors.
                mailbox.ResumeAfterFailedCompletion();
            }

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

    private async Task<ActorActivation> GetOrActivateAsync(ActorRuntimeRegistration registration, ActorId actorId, string activationId, CancellationToken cancellationToken)
    {
        var key = new ActorKey(registration.ActorType, actorId.Value);
        if (activations.TryGetValue(key, out var existing))
        {
            if (activationId.Length == 0 || string.Equals(existing.ActivationId, activationId, StringComparison.Ordinal))
            {
                return existing;
            }

            if (activations.TryRemove(KeyValuePair.Create(key, existing)))
            {
                logger.LogDebug("Activation id changed for {ActorType}/{ActorId}, deactivating stale activation.", registration.ActorType, actorId);
                try
                {
                    await DeactivateCoreAsync(existing, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Deactivating stale activation for {ActorType}/{ActorId} failed.", registration.ActorType, actorId);
                }
            }
        }

        var state = new ActorStateUnitOfWork(
            registration.ActorType,
            actorId,
            stateStore,
            serializer,
            stateFaultInjector,
            stateMigrator,
            registration.TypeOptions?.DisableStateMigration
                ?? (options.Value.DisableStateMigration || registration.Options?.DisableStateMigration == true));
        var activationContext = new ActorActivationContext(actorId, state);

        // The activation service provider layers the activation built-ins over a per-activation DI scope that
        // is created lazily on first use. Actors that depend only on those built-ins never allocate a scope.
        // The provider owns the scope's lifetime and is disposed with the activation, preserving disposal of
        // any scoped or transient services it resolves.
        var provider = new ActorActivationServiceProvider(scopeFactory, activationContext);
        ActorActivation activation;
        try
        {
            var actor = registration.Factory(provider, actorId);
            activation = new ActorActivation(registration.ActorType, actorId, actor, state, state, provider, registration.Lifecycle)
            {
                ActivationId = activationId,
            };
        }
        catch
        {
            try
            {
                await provider.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                ActivationEnded?.Invoke(registration.ActorType, actorId);
            }
            throw;
        }

        try
        {
            await activation.OnActivateAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await DisposeActivationAsync(activation).ConfigureAwait(false);
            throw;
        }

        // Mailbox admission serializes activation. Publish only after initialization succeeds,
        // so a failed or cancelled activation can never be reused by a later turn.
        if (!activations.TryAdd(key, activation))
        {
            await activation.DisposeAsync().ConfigureAwait(false);
            return activations[key];
        }

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
