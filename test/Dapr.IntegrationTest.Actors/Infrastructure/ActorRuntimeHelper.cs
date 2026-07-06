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

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dapr.IntegrationTest.Actors;

namespace Dapr.IntegrationTest.Actors.Infrastructure;

/// <summary>
/// Provides helpers for waiting until the Dapr actor runtime is ready to process requests.
/// </summary>
public static class ActorRuntimeHelper
{
    // Per-attempt timeout for each Ping call. Keeping this well below the HttpClient
    // default (100 s) prevents a hung placement-registration request from stalling
    // the poll loop for a full 100 seconds before the next retry.
    private static readonly TimeSpan PingAttemptTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Polls <paramref name="pingActor"/> until a <see cref="IPingActor.Ping"/> call succeeds,
    /// indicating that the actor runtime has registered the actor type with the placement service.
    /// </summary>
    /// <param name="pingActor">
    /// The actor proxy to use for health probing.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that cancels the polling loop.
    /// </param>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is cancelled before the runtime is ready.
    /// </exception>
    public static async Task WaitForActorRuntimeAsync(IPingActor pingActor, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Use a short per-attempt timeout so a hung request (e.g. while the Dapr
                // placement service is still registering the actor type) does not stall the
                // poll loop for the full HttpClient default of 100 seconds.
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                attemptCts.CancelAfter(PingAttemptTimeout);

                await pingActor.Ping(attemptCts.Token);
                return;
            }
            catch (DaprApiException)
            {
                // The actor runtime returned an error response – placement may not have
                // registered the actor type yet. Retry after a short pause.
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // The per-attempt timeout fired (not the outer cancellation token). The
                // sidecar accepted the TCP connection but did not respond in time – this
                // happens while Dapr is still acquiring a placement token. Retry.
            }
            catch (HttpRequestException)
            {
                // Connection-level error – the sidecar may still be starting up.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }
    }
}
