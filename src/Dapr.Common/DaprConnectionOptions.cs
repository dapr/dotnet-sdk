namespace Dapr.Common;

/// <summary>
/// Provides the options used to configure a Dapr client.
/// </summary>
public class DaprClientOptions
{
    /// <summary>
    /// Provides the endpoint and port to use as the gRPC endpoint.
    /// </summary>
    public string? GrpcEndpoint { get; init; }
    
    /// <summary>
    /// Only used fi the <see cref="GrpcEndpoint"/> isn't specified, this provides the port to use
    /// with a localhost gRPC endpoint address.
    /// </summary>
    public int? GrpcPort { get; init; }
    
    /// <summary>
    /// Provides the endpoint and port to use as the HTTP endpoint.
    /// </summary>
    public string? HttpEndpoint { get; init; }
    
    /// <summary>
    /// Only used if the <see cref="HttpEndpoint"/> isn't specified, this provides the port to use with a
    /// localhost HTTP endpoint address.
    /// </summary>
    public int? HttpPort { get; init; }
    
    /// <summary>
    /// The timeout used for all requests to the Dapr runtime.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// The Dapr API token to include on every request to the Dapr runtime.
    /// </summary>
    public string? DaprApiToken { get; init; }
}
