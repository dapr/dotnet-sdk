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
