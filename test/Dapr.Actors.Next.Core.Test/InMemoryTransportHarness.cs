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
using Dapr.Actors.Next.Core.Transport;

namespace Dapr.Actors.Next.Core.Test;

public sealed class InMemoryTransportHarness : ISubscribeActorEventsTransport
{
    private readonly Channel<InMemorySubscribeActorEventsStream> opened = Channel.CreateUnbounded<InMemorySubscribeActorEventsStream>();

    public ConcurrentBag<InMemorySubscribeActorEventsStream> Streams { get; } = new();

    public ValueTask<ISubscribeActorEventsStream> OpenStreamAsync(CancellationToken cancellationToken = default)
    {
        var stream = new InMemorySubscribeActorEventsStream();
        Streams.Add(stream);
        opened.Writer.TryWrite(stream);
        return ValueTask.FromResult<ISubscribeActorEventsStream>(stream);
    }

    public async Task<InMemorySubscribeActorEventsStream> WaitForStreamAsync(CancellationToken cancellationToken = default) =>
        await opened.Reader.ReadAsync(cancellationToken);
}

public sealed class InMemorySubscribeActorEventsStream : ISubscribeActorEventsStream
{
    private readonly Channel<SubscribeActorEventsRequest> requests = Channel.CreateUnbounded<SubscribeActorEventsRequest>();
    private readonly Channel<SubscribeActorEventsResponse> responses = Channel.CreateUnbounded<SubscribeActorEventsResponse>();

    public IAsyncEnumerable<SubscribeActorEventsRequest> ReadAllAsync(CancellationToken cancellationToken = default) =>
        requests.Reader.ReadAllAsync(cancellationToken);

    public ValueTask WriteAsync(SubscribeActorEventsResponse response, CancellationToken cancellationToken = default) =>
        responses.Writer.WriteAsync(response, cancellationToken);

    public ValueTask SendAsync(SubscribeActorEventsRequest request, CancellationToken cancellationToken = default) =>
        requests.Writer.WriteAsync(request, cancellationToken);

    public async Task<SubscribeActorEventsResponse> ReceiveAsync(CancellationToken cancellationToken = default) =>
        await responses.Reader.ReadAsync(cancellationToken);

    public void Disconnect() => requests.Writer.TryComplete();

    public ValueTask DisposeAsync()
    {
        requests.Writer.TryComplete();
        responses.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
