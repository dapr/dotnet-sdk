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

#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace Dapr.EntityFrameworkCore.Outbox;

/// <summary>
/// Drives the delivery of pending outbox messages to Dapr.
/// The default implementation is <see cref="PollingOutboxDispatcher{TDbContext}"/>; register
/// a custom implementation via <c>IDaprOutboxBuilder.UseDispatcher&lt;T&gt;()</c>.
/// </summary>
public interface IOutboxDispatcher
{
    /// <summary>
    /// Claims the next batch of pending messages and publishes them via <c>DaprClient</c>.
    /// Called on the polling loop's cadence by <c>DaprOutboxHostedService</c>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation for the dispatch iteration.</param>
    /// <returns>The number of messages processed (published or failed) in this iteration.</returns>
    Task<int> DispatchPendingAsync(CancellationToken cancellationToken);
}
