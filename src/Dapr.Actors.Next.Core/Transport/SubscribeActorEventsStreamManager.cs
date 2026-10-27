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
using System.Threading.Channels;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Exceptions;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Abstractions.Scheduling;
using Dapr.Actors.Next.Core;
using Dapr.Actors.Next.Core.Registration;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dapr.Actors.Next.Core.Transport;

/// <summary>
/// Hosted service that owns the app-initiated SubscribeActorEvents stream.
/// </summary>
/// <remarks>
/// Callback admission is not globally bounded: an accepted turn can await another callback on the
/// same stream. Memory therefore scales with outstanding calls, while actor mailboxes enforce turn
/// serialization. Disconnects cancel accepted work; cancellation-ignoring actor code can outlive its
/// stream, but its eventual completion is still observed.
/// </remarks>
public sealed class SubscribeActorEventsStreamManager(
    ISubscribeActorEventsTransport transport,
    ActorRuntimeRegistry registry,
    IActorRuntime runtime,
    TimeProvider timeProvider,
    IOptions<DaprActorsOptions> options,
    ILogger<SubscribeActorEventsStreamManager> logger) : BackgroundService, IAsyncDisposable
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);
    private readonly ActorActivationIdGate? activationIdGate;
    private readonly ConcurrentDictionary<string, StreamLease> streams = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource shutdown = new();
    private readonly object disposalLock = new();
    private Task? disposal;

    internal SubscribeActorEventsStreamManager(
        ISubscribeActorEventsTransport transport,
        ActorRuntimeRegistry registry,
        IActorRuntime runtime,
        TimeProvider timeProvider,
        IOptions<DaprActorsOptions> options,
        ILogger<SubscribeActorEventsStreamManager> logger,
        ActorActivationIdGate activationIdGate)
        : this(transport, registry, runtime, timeProvider, options, logger)
    {
        this.activationIdGate = activationIdGate;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (disposalLock)
            return new ValueTask(disposal ??= DisposeCoreAsync());
    }

    /// <inheritdoc />
    public override void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private async Task DisposeCoreAsync()
    {
        await Task.Yield();
        await shutdown.CancelAsync().ConfigureAwait(false);
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        var leases = streams.ToArray();
        streams.Clear();
        await Task.WhenAll(leases.Select(pair => pair.Value.DisposeAsync(CancellationToken.None).AsTask())).ConfigureAwait(false);
        base.Dispose();
        shutdown.Dispose();
    }

    /// <summary>
    /// Opens the stream for a newly hosted actor type.
    /// </summary>
    internal ValueTask OpenStreamForTypeAsync(string actorType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        if (streams.ContainsKey(actorType))
        {
            return ValueTask.CompletedTask;
        }

        var streamCts = new StreamCancellation(shutdown.Token, cancellationToken);
        var streamToken = streamCts.Token;
        var task = Task.Run(() => RunTypeStreamLoopAsync(actorType, streamToken), CancellationToken.None);
        Observe(task);
        if (!streams.TryAdd(actorType, new StreamLease(streamCts, task)))
        {
            Observe(streamCts.CancelAsync());
            streamCts.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Adds a dynamic actor type to the live registry and opens its stream.
    /// </summary>
    internal async ValueTask<bool> OpenStreamForRegistrationAsync(ActorRuntimeRegistration registration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);
        if (!registry.TryAdd(registration))
        {
            return false;
        }

        await OpenStreamForTypeAsync(registration.ActorType, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Closes the stream for a hosted actor type and lets in-flight turns drain.
    /// </summary>
    internal async ValueTask CloseStreamForTypeAsync(string actorType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        if (!streams.TryRemove(actorType, out var lease))
        {
            registry.TryRemove(actorType);
            return;
        }

        await lease.DisposeAsync(cancellationToken).ConfigureAwait(false);
        registry.TryRemove(actorType);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var stoppingRegistration = stoppingToken.Register(static state => ((CancellationTokenSource)state!).Cancel(), shutdown);
        foreach (var actorType in registry.ActorTypes)
        {
            await OpenStreamForTypeAsync(actorType, stoppingToken).ConfigureAwait(false);
        }

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, timeProvider, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            shutdown.Cancel();
            var leases = streams.ToArray();
            streams.Clear();
            await Task.WhenAll(leases.Select(static pair => pair.Value.DisposeAsync(CancellationToken.None).AsTask())).ConfigureAwait(false);
        }
    }

    private async Task RunTypeStreamLoopAsync(string actorType, CancellationToken stoppingToken)
    {
        var registration = registry.GetByActorType(actorType);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var streamCancellation = new StreamCancellation(stoppingToken);
                var stream = await transport.OpenStreamAsync(streamCancellation.Token).ConfigureAwait(false);
                await RunStreamAsync(registration, stream, streamCancellation, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "SubscribeActorEvents stream for {ActorType} disconnected; reconnecting.", actorType);
                await DelayOrStopAsync(ReconnectDelay, timeProvider, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task DelayOrStopAsync(TimeSpan delay, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, timeProvider, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task RunStreamAsync(
        ActorRuntimeRegistration registration,
        ISubscribeActorEventsStream stream,
        StreamCancellation streamCancellation,
        CancellationToken stoppingToken)
    {
        var cancellationToken = streamCancellation.Token;
        var outbound = Channel.CreateUnbounded<SubscribeActorEventsResponse>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
        outbound.Writer.TryWrite(SubscribeActorEventsResponse.RegisteredActors(
            [registration.ActorType],
            CreateInitialConfig(registration.Options ?? options.Value, registration.TypeOptions)));
        var dispatches = new InFlightDispatches();
        var writer = PumpWriterAsync(stream, outbound.Reader, cancellationToken);
        var reader = Task.Run(async () =>
        {
            await foreach (var request in stream.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                // A callback can await another callback on this stream. Neither a worker limit nor a
                // bounded inbound queue may hold that child behind its waiting parent. Actor mailboxes
                // serialize turns; transport memory necessarily scales with accepted outstanding calls.
                dispatches.Add(Task.Run(async () =>
                {
                    var response = await DispatchOneAsync(request, cancellationToken).ConfigureAwait(false);
                    await outbound.Writer.WriteAsync(response, cancellationToken).ConfigureAwait(false);
                }, cancellationToken));
            }
        }, cancellationToken);

        try
        {
            await SuperviseAsync(reader, writer, dispatches.Failure, cancellationToken).ConfigureAwait(false);
            // EOF is graceful: finish accepted turns, then flush their responses through the sole writer.
            await SuperviseAsync(dispatches.DrainAsync(), writer, dispatches.Failure, cancellationToken).ConfigureAwait(false);
            outbound.Writer.TryComplete();
            await writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Cancel the call-level token before disposing: the gRPC transport completes its request
            // stream during disposal, which otherwise can wait behind a failed or blocked write.
            Observe(streamCancellation.CancelAsync());
            outbound.Writer.TryComplete();
            Observe(reader);
            Observe(writer);
            // Actor code and third-party transports may ignore cancellation. Do not keep a failed
            // stream alive waiting for actor code; late completions remain observed by the supervisor.
            var disposal = stream.DisposeAsync().AsTask();
            Observe(disposal);
            await disposal.WaitAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private static async Task SuperviseAsync(Task operation, Task writer, Task<Exception> failure, CancellationToken cancellationToken)
    {
        var completed = await Task.WhenAny(operation, writer, failure).WaitAsync(cancellationToken).ConfigureAwait(false);
        if (completed == failure)
        {
            throw await failure.ConfigureAwait(false);
        }

        await completed.ConfigureAwait(false);
    }

    private static void Observe(Task task) =>
        _ = task.ContinueWith(static completed => _ = completed.Exception,
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    private sealed class InFlightDispatches
    {
        private readonly HashSet<Task> tasks = [];
        private readonly TaskCompletionSource<Exception> failure = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<Exception> Failure => failure.Task;

        public void Add(Task task)
        {
            lock (tasks)
            {
                tasks.Add(task);
            }

            _ = task.ContinueWith(completed =>
            {
                lock (tasks)
                {
                    if (completed.Exception is { } exception)
                    {
                        failure.TrySetResult(exception);
                    }

                    tasks.Remove(completed);
                }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        public Task DrainAsync()
        {
            lock (tasks)
            {
                var drain = Task.WhenAll(tasks);
                Observe(drain);
                return drain;
            }
        }
    }

    private async Task<SubscribeActorEventsResponse> DispatchOneAsync(SubscribeActorEventsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Actor callbacks are at-least-once. State is saved inside DispatchAsync before this response
            // is queued, so a dropped stream after commit can re-run the turn against already durable state.
            var gated = await GateActivationIdAsync(request, cancellationToken).ConfigureAwait(false);
            var response = await runtime.DispatchAsync(ToRuntimeRequest(gated), cancellationToken).ConfigureAwait(false);
            return new SubscribeActorEventsResponse(
                request.Id,
                request.Kind,
                response ?? ReadOnlyMemory<byte>.Empty,
                ActorHeaders.Empty,
                Cancel: ShouldCancelTimerAfterSuccess(request));
        }
        catch (ActorInvocationException ex) when (request.Kind == SubscribeActorEventsFrameKind.Invoke)
        {
            var payload = System.Text.Encoding.UTF8.GetBytes(ex.Message);
            return new SubscribeActorEventsResponse(request.Id, request.Kind, payload, ActorHeaders.Empty, Error: true);
        }
        catch (Exception ex)
        {
            if (request.Kind is SubscribeActorEventsFrameKind.Reminder or SubscribeActorEventsFrameKind.Timer)
            {
                logger.LogError(ex, "Reminder or timer callback {RequestId} failed; daprd will re-fire it until acknowledged.", request.Id);
            }

            return SubscribeActorEventsResponse.Failed(request.Id, ex.Message);
        }
    }

    private static async Task PumpWriterAsync(ISubscribeActorEventsStream stream, ChannelReader<SubscribeActorEventsResponse> outbound, CancellationToken cancellationToken)
    {
        await foreach (var response in outbound.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            await stream.WriteAsync(response, cancellationToken).ConfigureAwait(false);
        }
    }

    // Older sidecars forward caller metadata, so the id is only trusted from a sidecar that overwrites it.
    private async ValueTask<SubscribeActorEventsRequest> GateActivationIdAsync(SubscribeActorEventsRequest request, CancellationToken cancellationToken)
    {
        var activationId = ActorHeaders.GetActivationId(request.Headers);
        if (activationId.Length == 0)
        {
            return request;
        }

        if (activationIdGate is not null
            && await activationIdGate.FilterAsync(activationId, cancellationToken).ConfigureAwait(false) is not null)
        {
            return request;
        }

        return request with { Headers = ActorHeaders.WithoutActivationId(request.Headers) };
    }

    private static ActorRuntimeRequest ToRuntimeRequest(SubscribeActorEventsRequest request)
    {
        var turnKind = request.Kind switch
        {
            SubscribeActorEventsFrameKind.Invoke => ActorTurnKind.Invoke,
            SubscribeActorEventsFrameKind.Reminder => ActorTurnKind.Reminder,
            SubscribeActorEventsFrameKind.Timer => ActorTurnKind.Timer,
            SubscribeActorEventsFrameKind.Deactivate => ActorTurnKind.Deactivate,
            _ => ActorTurnKind.Invoke,
        };
        var context = new ActorRequestContext(
            request.Headers.GetValueOrDefault("traceparent"),
            request.Headers.GetValueOrDefault("tracestate"),
            request.Headers);
        return new ActorRuntimeRequest(request.ActorType, ActorId.Create(request.ActorId), request.MethodName, turnKind, request.Payload, request.Headers, context);
    }

    private static bool ShouldCancelTimerAfterSuccess(SubscribeActorEventsRequest request)
    {
        if (request.Kind != SubscribeActorEventsFrameKind.Timer)
        {
            return false;
        }

        if (!request.Headers.TryGetValue("dapr-period", out var period) || string.IsNullOrWhiteSpace(period))
        {
            return true;
        }

        return IsZeroDuration(period);
    }

    private static bool IsZeroDuration(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Equals("0", StringComparison.Ordinal)
            || trimmed.Equals("0ms", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("0s", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("PT0S", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static SubscribeActorEventsInitialConfig CreateInitialConfig(DaprActorsOptions options, DaprActorTypeOptions? typeOptions) =>
        new(
            typeOptions?.IdleTimeout ?? options.ActorIdleTimeout,
            typeOptions?.DrainOngoingCallTimeout ?? options.DrainOngoingCallTimeout,
            typeOptions?.DrainRebalancedActors ?? options.DrainRebalancedActors,
            typeOptions?.EnableReentrancy ?? options.EnableReentrancy,
            typeOptions?.MaxReentrantDepth ?? options.MaxReentrantDepth);

    internal sealed class StreamCancellation : IDisposable
    {
        private readonly CancellationTokenSource source = new();
        private readonly object sync = new();
        private readonly CancellationTokenRegistration stoppingRegistration;
        private readonly CancellationTokenRegistration cancellationRegistration;
        private Task? cancellation;

        public StreamCancellation(CancellationToken stoppingToken, CancellationToken cancellationToken = default)
        {
            stoppingRegistration = stoppingToken.UnsafeRegister(
                static state => Observe(((StreamCancellation)state!).CancelAsync()), this);
            cancellationRegistration = cancellationToken.UnsafeRegister(
                static state => Observe(((StreamCancellation)state!).CancelAsync()), this);
        }

        public CancellationToken Token => source.Token;

        public Task CancelAsync()
        {
            lock (sync)
            {
                return cancellation ??= source.CancelAsync();
            }
        }

        public void Dispose() => Observe(DisposeAfterCancellationAsync());

        private async Task DisposeAfterCancellationAsync()
        {
            try
            {
                // CancelAsync queues callbacks. Disposing the source before that task finishes can
                // discard registrations before they run, leaving otherwise cooperative turns stuck.
                // All cancellation paths share this task, including parent-token cancellation.
                await CancelAsync().ConfigureAwait(false);
            }
            finally
            {
                stoppingRegistration.Dispose();
                cancellationRegistration.Dispose();
                source.Dispose();
            }
        }
    }

    private sealed record StreamLease(StreamCancellation Cancellation, Task Task)
    {
        public async ValueTask DisposeAsync(CancellationToken cancellationToken)
        {
            var cancellation = Cancellation.CancelAsync();
            Observe(cancellation);
            try
            {
                await cancellation.WaitAsync(cancellationToken).ConfigureAwait(false);
                await Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                Cancellation.Dispose();
            }
        }
    }
}
