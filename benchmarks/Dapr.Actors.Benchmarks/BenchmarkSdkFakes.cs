using System.Net;
using Dapr.Actors.Next.Core.Client;

namespace Dapr.Actors.Benchmarks;

internal sealed class BenchmarkNoopActorInvocationClient : IActorInvocationClient
{
    private static readonly byte[] OneJsonPayload = "1"u8.ToArray();

    public Task<byte[]?> InvokeAsync(
        string actorType,
        string actorId,
        string methodName,
        ReadOnlyMemory<byte> payload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<byte[]?>(OneJsonPayload);
    }
}

internal sealed class BenchmarkNoopActorHttpMessageHandler : HttpMessageHandler
{
    private static readonly byte[] OneJsonPayload = "1"u8.ToArray();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(OneJsonPayload),
        };

        return Task.FromResult(response);
    }
}
