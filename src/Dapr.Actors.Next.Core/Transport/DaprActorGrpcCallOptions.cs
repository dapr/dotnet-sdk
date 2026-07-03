using System.Net.Http.Headers;
using System.Reflection;
using Grpc.Core;

namespace Dapr.Actors.Next.Core.Transport;

internal static class DaprActorGrpcCallOptions
{
    internal static CallOptions Create(string? daprApiToken, CancellationToken cancellationToken = default)
    {
        var headers = new Metadata();
        headers.Add("User-Agent", GetUserAgent());

        if (!string.IsNullOrWhiteSpace(daprApiToken))
        {
            headers.Add("dapr-api-token", daprApiToken);
        }

        return new CallOptions(headers: headers, cancellationToken: cancellationToken);
    }

    private static string GetUserAgent()
    {
        var assemblyVersion = typeof(DaprActorGrpcCallOptions).Assembly
            .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?
            .InformationalVersion;

        return new ProductInfoHeaderValue("dapr-sdk-dotnet", $"v{assemblyVersion}").ToString();
    }
}
