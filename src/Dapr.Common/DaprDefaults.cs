// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

using Microsoft.Extensions.Configuration;

namespace Dapr;

internal static class DaprDefaults
{
    private static string httpEndpoint = string.Empty;
    private static string grpcEndpoint = string.Empty;
    private static string daprApiToken = string.Empty;
    private static string appApiToken = string.Empty;

    public const string DaprApiTokenName = "DAPR_API_TOKEN";
    public const string AppApiTokenName = "APP_API_TOKEN";
    public const string DaprHttpEndpointName = "DAPR_HTTP_ENDPOINT";
    public const string DaprHttpPortName = "DAPR_HTTP_PORT";
    public const string DaprGrpcEndpointName = "DAPR_GRPC_ENDPOINT";
    public const string DaprGrpcPortName = "DAPR_GRPC_PORT";

    public const string DefaultDaprScheme = "http";
    public const string DefaultDaprHost = "localhost";
    public const int DefaultHttpPort = 3500;
    public const int DefaultGrpcPort = 50001;

    // Canonical gRPC URI scheme per https://github.com/grpc/grpc/blob/master/doc/naming.md.
    // The SDK accepts dns://host:port?tls=<bool> in addition to http(s) and rewrites it to
    // http(s) before handing the URI to GrpcChannel.ForAddress (which is HttpClient-backed
    // and does not understand the dns scheme natively).
    private const string DnsScheme = "dns";
    private const string TlsQueryParameter = "tls";

    /// <summary>
    /// Get the value of environment variable DAPR_API_TOKEN
    /// </summary>
    /// <param name="configuration">The optional <see cref="IConfiguration"/> to pull the value from.</param>
    /// <returns>The value of environment variable DAPR_API_TOKEN</returns>
    public static string GetDefaultDaprApiToken(IConfiguration? configuration) =>
        GetResourceValue(configuration, DaprApiTokenName) ?? string.Empty;

    /// <summary>
    /// Get the value of environment variable APP_API_TOKEN
    /// </summary>
    /// <param name="configuration">The optional <see cref="IConfiguration"/> to pull the value from.</param>
    /// <returns>The value of environment variable APP_API_TOKEN</returns>
    public static string GetDefaultAppApiToken(IConfiguration? configuration) =>
        GetResourceValue(configuration, AppApiTokenName) ?? string.Empty;

    /// <summary>
    /// Get the value of HTTP endpoint based off environment variables
    /// </summary>
    /// <param name="configuration">The optional <see cref="IConfiguration"/> to pull the value from.</param>
    /// <returns>The value of HTTP endpoint based off environment variables</returns>
    public static string GetDefaultHttpEndpoint(IConfiguration? configuration = null)
    {
        //Prioritize pulling from the IConfiguration and fallback to the environment variable if not populated
        var endpoint = GetResourceValue(configuration, DaprHttpEndpointName);
        var port = GetResourceValue(configuration, DaprHttpPortName);
            
        //Use the default HTTP port if we're unable to retrieve/parse the provided port
        int? parsedGrpcPort = string.IsNullOrWhiteSpace(port) ? DefaultHttpPort : int.Parse(port);

        return BuildEndpoint(endpoint, parsedGrpcPort.Value);
    }

    /// <summary>
    /// Get the value of gRPC endpoint based off environment variables
    /// </summary>
    /// <param name="configuration">The optional <see cref="IConfiguration"/> to pull the value from.</param>
    /// <returns>The value of gRPC endpoint based off environment variables</returns>
    public static string GetDefaultGrpcEndpoint(IConfiguration? configuration = null)
    {
        //Prioritize pulling from the IConfiguration and fallback to the environment variable if not populated
        var endpoint = GetResourceValue(configuration, DaprGrpcEndpointName);
        var port = GetResourceValue(configuration, DaprGrpcPortName);

        //Use the default gRPC port if we're unable to retrieve/parse the provided port
        int? parsedGrpcPort = string.IsNullOrWhiteSpace(port) ? DefaultGrpcPort : int.Parse(port);

        // Rewrite the canonical gRPC URI form (dns://host:port?tls=<bool>) to http/https before
        // BuildEndpoint strips the query string when reassembling via UriBuilder.
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = NormalizeGrpcEndpoint(endpoint);
        }

        return BuildEndpoint(endpoint, parsedGrpcPort.Value);
    }

    /// <summary>
    /// Translates a gRPC endpoint into the http/https form that <see cref="Grpc.Net.Client.GrpcChannel.ForAddress(string)"/>
    /// understands. Accepts:
    /// <list type="bullet">
    /// <item><description><c>http://host:port</c> and <c>https://host:port</c> — returned unchanged.</description></item>
    /// <item><description>The canonical gRPC URI form <c>dns://host:port?tls=&lt;bool&gt;</c> — rewritten to
    /// <c>https://host:port</c> when <c>tls=true</c> and <c>http://host:port</c> when <c>tls=false</c>.</description></item>
    /// </list>
    /// Throws <see cref="InvalidOperationException"/> for any other scheme, or for a <c>dns</c> URI without
    /// a parseable <c>tls</c> query parameter.
    /// </summary>
    internal static string NormalizeGrpcEndpoint(string endpoint)
    {
        var uri = new Uri(endpoint, UriKind.Absolute);

        if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        {
            return endpoint;
        }

        if (uri.Scheme == DnsScheme)
        {
            var useTls = TryParseTlsQuery(uri.Query);
            if (useTls is null)
            {
                throw new InvalidOperationException(
                    $"The gRPC endpoint '{endpoint}' uses the 'dns' scheme but is missing or has an invalid 'tls' " +
                    "query parameter. Use the canonical gRPC URI form 'dns://host:port?tls=true' or 'dns://host:port?tls=false'.");
            }

            var scheme = useTls.Value ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
            return new UriBuilder { Scheme = scheme, Host = uri.Host, Port = uri.Port }.ToString();
        }

        throw new InvalidOperationException(
            "The gRPC endpoint must use 'http', 'https', or the canonical gRPC URI form 'dns://host:port?tls=<bool>'.");
    }

    private static bool? TryParseTlsQuery(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            return null;
        }

        var trimmed = query.StartsWith("?") ? query.Substring(1) : query;
        foreach (var pair in trimmed.Split('&'))
        {
            if (pair.Length == 0)
            {
                continue;
            }

            var eq = pair.IndexOf('=');
            if (eq < 0)
            {
                continue;
            }

            var key = pair.Substring(0, eq);
            if (!string.Equals(key, TlsQueryParameter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = pair.Substring(eq + 1);
            return bool.TryParse(value, out var parsed) ? parsed : null;
        }

        return null;
    }

    /// <summary>
    /// Builds the Dapr endpoint.
    /// </summary>
    /// <param name="endpoint">The endpoint value.</param>
    /// <param name="endpointPort">The endpoint port value, whether pulled from configuration/envvar or the default.</param>
    /// <returns>A constructed endpoint value.</returns>
    private static string BuildEndpoint(string? endpoint, int endpointPort)
    {
        var endpointBuilder = new UriBuilder { Scheme = DefaultDaprScheme, Host = DefaultDaprHost }; //Port depends on endpoint

        if (!string.IsNullOrWhiteSpace(endpoint)) //If the endpoint is set, it doesn't matter if the port is
        {
            //Extract the scheme, host and port from the endpoint and replace defaults
            var uri = new Uri(endpoint);
            endpointBuilder.Scheme = uri.Scheme;
            endpointBuilder.Host = uri.Host;
            endpointBuilder.Port = uri.Port;
        }
        else
        {
            //Should only set the port if the endpoint isn't populated
            endpointBuilder.Port = endpointPort;
        }

        return endpointBuilder.ToString();
    }
        
    /// <summary>
    /// Retrieves the specified value prioritizing pulling it from <see cref="IConfiguration"/>, falling back
    /// to an environment variable, and using an empty string as a default.
    /// </summary>
    /// <param name="configuration">An instance of an <see cref="IConfiguration"/>.</param>
    /// <param name="name">The name of the value to retrieve.</param>
    /// <returns>The value of the resource.</returns>
    private static string? GetResourceValue(IConfiguration? configuration, string name)
    {
        //Attempt to retrieve first from the configuration
        var configurationValue = configuration?[name];
        if (configurationValue is not null)
        {
            return configurationValue;
        }

        //Fall back to the environment variable with the same name or default to an empty string
        return Environment.GetEnvironmentVariable(name);
    }
}
