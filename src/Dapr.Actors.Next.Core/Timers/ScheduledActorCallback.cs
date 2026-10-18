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

using System.Diagnostics;
using Dapr.Actors.Next.Core.Runtime;
using Microsoft.Extensions.Logging;

namespace Dapr.Actors.Next.Core.Timers;

internal sealed class ScheduledActorCallback
{
    private readonly object sync;
    private readonly IActorRuntime runtime;
    private readonly TimeProvider timeProvider;
    private readonly ActorRuntimeRequest request;
    private readonly DateTimeOffset? expiresAt;
    private readonly ILogger? logger;
    private readonly ITimer timer;
    private bool active;

    public ScheduledActorCallback(
        object sync,
        IActorRuntime runtime,
        TimeProvider timeProvider,
        ActorRuntimeRequest request,
        DateTimeOffset? expiresAt,
        ILogger? logger)
    {
        this.sync = sync;
        this.runtime = runtime;
        this.timeProvider = timeProvider;
        this.request = request;
        this.expiresAt = expiresAt;
        this.logger = logger;

        // System timers otherwise retain the registering actor's AsyncLocal call chain.
        if (ExecutionContext.IsFlowSuppressed())
        {
            timer = CreateTimer();
        }
        else
        {
            using (ExecutionContext.SuppressFlow())
            {
                timer = CreateTimer();
            }
        }
    }

    // Called under the scheduler lock, after publishing the registration.
    public void Start(TimeSpan dueTime, TimeSpan period)
    {
        active = true;
        if (!timer.Change(dueTime, period))
        {
            throw new InvalidOperationException("The actor callback timer could not be started.");
        }
    }

    // Cancellation prevents new admissions, but does not interrupt an already admitted turn.
    public void Stop()
    {
        active = false;
        timer.Dispose();
    }

    public static IReadOnlyDictionary<string, string> IndependentHeaders(IReadOnlyDictionary<string, string>? headers) =>
        headers is null
            ? ActorHeaders.Empty
            : headers.Where(static pair =>
                    !string.Equals(pair.Key, "Dapr-Reentrancy-Id", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(pair.Key, "dapr-reentrant-id", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(pair.Key, "x-dapr-reentrant-id", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(static pair => pair.Key, static pair => pair.Value);

    private ITimer CreateTimer() =>
        timeProvider.CreateTimer(
            static state =>
            {
                var callback = (ScheduledActorCallback)state!;
                // A custom provider may invoke its callback inline in Change. Defer only that
                // case; virtual providers must admit turns before advancing to the next due time.
                if (Monitor.IsEntered(callback.sync))
                {
                    ThreadPool.UnsafeQueueUserWorkItem(
                        static (ScheduledActorCallback queued) => _ = queued.DispatchAsync(),
                        callback,
                        preferLocal: false);
                }
                else
                {
                    _ = callback.DispatchAsync();
                }
            },
            this,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);

    private async Task DispatchAsync()
    {
        try
        {
            lock (sync)
            {
                if (!active)
                {
                    return;
                }

                if (expiresAt.HasValue && timeProvider.GetUtcNow() > expiresAt.Value)
                {
                    Stop();
                    return;
                }
            }

            await runtime.DispatchAsync(request, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            if (logger is not null)
            {
                logger.LogError(exception, "Actor scheduled callback failed for {ActorType}/{ActorId}/{OperationName}.",
                    request.ActorType, request.ActorId, request.OperationName);
            }
            else
            {
                Trace.TraceError("Actor scheduled callback failed for {0}/{1}/{2}: {3}",
                    request.ActorType, request.ActorId, request.OperationName, exception);
            }
        }
    }
}
