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

using System.Runtime.CompilerServices;
using Dapr.Actors.Next.Core.Transport;

namespace Dapr.Actors.Next.Testing;

/// <summary>
/// Decorates an in-memory or real Dapr callback transport with faults controlled by <see cref="ActorFaults"/>.
/// Register this as the host's <see cref="ISubscribeActorEventsTransport"/> to test reconnects and response loss.
/// </summary>
public sealed class FaultInjectingActorEventsTransport : ISubscribeActorEventsTransport
{
    private readonly ISubscribeActorEventsTransport inner;
    private readonly ActorFaults faults;

    /// <summary>
    /// Creates a fault-injecting decorator. The underlying stream retains its normal ownership and lifetime.
    /// </summary>
    public FaultInjectingActorEventsTransport(ISubscribeActorEventsTransport inner, ActorFaults faults)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.faults = faults ?? throw new ArgumentNullException(nameof(faults));
    }

    /// <inheritdoc />
    public async ValueTask<ISubscribeActorEventsStream> OpenStreamAsync(CancellationToken cancellationToken = default) =>
        new Stream(await inner.OpenStreamAsync(cancellationToken).ConfigureAwait(false), faults);

    private sealed class Stream(ISubscribeActorEventsStream inner, ActorFaults faults) : ISubscribeActorEventsStream
    {
        public async IAsyncEnumerable<SubscribeActorEventsRequest> ReadAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var request in inner.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                faults.BeforeStreamFrame(write: false, request.Kind);
                yield return request;
            }
        }

        public ValueTask WriteAsync(SubscribeActorEventsResponse response, CancellationToken cancellationToken = default)
        {
            faults.BeforeStreamFrame(write: true, response.Kind);
            return inner.WriteAsync(response, cancellationToken);
        }

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
