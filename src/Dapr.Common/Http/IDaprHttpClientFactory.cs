namespace Dapr.Common.Http;

/// <summary>
/// Factory for creating Dapr-configured <see cref="HttpClient"/> instances.
/// </summary>
public interface IDaprHttpClientFactory
{
    /// <summary>
    /// Produces a Dapr-configured <see cref="HttpClient"/> instance.
    /// </summary>
    /// <returns></returns>
    HttpClient CreateClient();
}
