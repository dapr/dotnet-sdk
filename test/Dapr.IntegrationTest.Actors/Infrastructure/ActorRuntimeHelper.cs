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
using System.Threading;
using System.Threading.Tasks;
using Dapr.IntegrationTest.Actors;

namespace Dapr.IntegrationTest.Actors.Infrastructure;

/// <summary>
/// Provides helpers for waiting until the Dapr actor runtime is ready to process requests.
/// </summary>
public static class ActorRuntimeHelper
{
    /// <summary>
    /// Polls <paramref name="pingActor"/> until a <see cref="IPingActor.Ping"/> call succeeds,
    /// indicating that the actor runtime has registered the actor type with the placement service.
    /// </summary>
    /// <param name="pingActor">The actor proxy to use for health probing.</param>
    /// <param name="cancellationToken">A token that cancels the polling loop.</param>
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
                await pingActor.Ping();
                return;
            }
            catch (DaprApiException)
            {
                // The actor runtime is not yet ready – wait a short interval and retry.
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }
        }
    }
}
