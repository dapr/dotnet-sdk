namespace Dapr.Common.Http;

/// <summary>
/// Concrete implementation of a <see cref="IDaprHttpClientFactory"/>.
/// </summary>
public sealed class DefaultDaprHttpClientFactory(IHttpClientFactory httpClientFactory, Action<HttpClient> configure) : IDaprHttpClientFactory
{
    /// <inheritdoc />   
    public HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient();
        configure(client);
        return client;
    }
}
