using Google.Protobuf;
using Dapr.Actors.Next.Core;
using Dapr.Actors.Next.Core.Transport;
using P = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Actors.Next.Core.Client;

/// <summary>
/// Sends actor invocations through the generated Dapr gRPC actor invocation API.
/// </summary>
public sealed class DaprActorInvocationClient : IActorInvocationClient
{
    private readonly Lazy<P.Dapr.DaprClient> client;
    private readonly string? daprApiToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprActorInvocationClient"/> class.
    /// </summary>
    public DaprActorInvocationClient(P.Dapr.DaprClient client, string? daprApiToken = null)
        : this(CreateEagerAccessor(client), daprApiToken)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprActorInvocationClient"/> class whose Dapr gRPC
    /// client is resolved on first use, so constructing the client does not eagerly build the transport channel.
    /// </summary>
    public DaprActorInvocationClient(Lazy<P.Dapr.DaprClient> client, string? daprApiToken = null)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.daprApiToken = daprApiToken;
    }

    private static Lazy<P.Dapr.DaprClient> CreateEagerAccessor(P.Dapr.DaprClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        return new Lazy<P.Dapr.DaprClient>(() => client);
    }

    /// <inheritdoc />
    public async Task<byte[]?> InvokeAsync(
        string actorType,
        string actorId,
        string methodName,
        ReadOnlyMemory<byte> payload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        headers = ActorHeaders.WithCurrentReentrancy(headers);

        var request = new P.InvokeActorRequest
        {
            ActorType = actorType,
            ActorId = actorId,
            Method = methodName,
            Data = ByteString.CopyFrom(payload.Span),
        };

        foreach (var (key, value) in headers)
        {
            request.Metadata[key] = value;
        }

        var response = await client.Value.InvokeActorAsync(
            request,
            DaprActorGrpcCallOptions.Create(daprApiToken, cancellationToken)).ConfigureAwait(false);
        return response.Data.IsEmpty ? null : response.Data.ToByteArray();
    }
}
