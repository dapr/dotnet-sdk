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

        return BuildEndpoint(endpoint, parsedGrpcPort.Value);
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
