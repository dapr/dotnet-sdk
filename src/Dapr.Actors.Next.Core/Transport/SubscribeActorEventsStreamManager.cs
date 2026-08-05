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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dapr.Actors.Next.Core.Transport;

/// <summary>
/// Hosted service that owns the app-initiated SubscribeActorEvents stream.
/// </summary>
public sealed class SubscribeActorEventsStreamManager(
    ISubscribeActorEventsTransport transport,
    ActorRuntimeRegistry registry,
    IActorRuntime runtime,
    TimeProvider timeProvider,
    IOptions<DaprActorsOptions> options,
    ILogger<SubscribeActorEventsStreamManager> logger) : BackgroundService
{
    private const int DefaultBackpressureCapacity = 64;
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);
    private readonly ConcurrentDictionary<string, StreamLease> streams = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource shutdown = new();

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

        var streamCts = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token, cancellationToken);
        var task = Task.Run(() => RunTypeStreamLoopAsync(actorType, streamCts.Token), CancellationToken.None);
        if (!streams.TryAdd(actorType, new StreamLease(streamCts, task)))
        {
            streamCts.Cancel();
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
                await using var stream = await transport.OpenStreamAsync(stoppingToken).ConfigureAwait(false);
                await RunStreamAsync(registration, stream, stoppingToken).ConfigureAwait(false);
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

    private async Task RunStreamAsync(ActorRuntimeRegistration registration, ISubscribeActorEventsStream stream, CancellationToken stoppingToken)
    {
        var inbound = Channel.CreateBounded<SubscribeActorEventsRequest>(new BoundedChannelOptions(DefaultBackpressureCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true,
        });
        var outbound = Channel.CreateUnbounded<SubscribeActorEventsResponse>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
        var writer = PumpWriterAsync(stream, outbound.Reader, stoppingToken);
        var workerCount = Math.Max(1, Math.Min(Environment.ProcessorCount, 8));
        var workers = Enumerable.Range(0, workerCount)
            .Select(_ => ProcessInboundAsync(inbound.Reader, outbound.Writer, stoppingToken))
            .ToArray();

        try
        {
            await outbound.Writer.WriteAsync(
                SubscribeActorEventsResponse.RegisteredActors(
                    [registration.ActorType],
                    CreateInitialConfig(registration.Options ?? options.Value, registration.TypeOptions)),
                stoppingToken).ConfigureAwait(false);

            await foreach (var request in stream.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                await inbound.Writer.WriteAsync(request, stoppingToken).ConfigureAwait(false);
            }
        }
        finally
        {
            inbound.Writer.TryComplete();
            await Task.WhenAll(workers).ConfigureAwait(false);
            outbound.Writer.TryComplete();
            await writer.ConfigureAwait(false);
        }
    }

    private async Task ProcessInboundAsync(ChannelReader<SubscribeActorEventsRequest> reader, ChannelWriter<SubscribeActorEventsResponse> writer, CancellationToken cancellationToken)
    {
        await foreach (var request in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            await writer.WriteAsync(await DispatchOneAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<SubscribeActorEventsResponse> DispatchOneAsync(SubscribeActorEventsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Actor callbacks are at-least-once. State is saved inside DispatchAsync before this response
            // is queued, so a dropped stream after commit can re-run the turn against already durable state.
            var response = await runtime.DispatchAsync(ToRuntimeRequest(request), cancellationToken).ConfigureAwait(false);
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

    private sealed record StreamLease(CancellationTokenSource Cancellation, Task Task)
    {
        public async ValueTask DisposeAsync(CancellationToken cancellationToken)
        {
            await Cancellation.CancelAsync().ConfigureAwait(false);
            try
            {
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
