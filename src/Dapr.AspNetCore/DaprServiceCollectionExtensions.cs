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

#nullable enable

namespace Microsoft.Extensions.DependencyInjection;

using System;
using Dapr;
using Dapr.Client;
using Extensions;
using Configuration;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection" />.
/// </summary>
public static class DaprServiceCollectionExtensions
{
    /// <summary>
    /// Adds Dapr client services to the provided <see cref="IServiceCollection" />. This does not include integration
    /// with ASP.NET Core MVC. Use the <c>AddDapr()</c> extension method on <c>IMvcBuilder</c> to register MVC integration.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" />.</param>
    /// <param name="configure"></param>
    public static void AddDaprClient(this IServiceCollection services, Action<DaprClientBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        services.TryAddSingleton(serviceProvider =>
        {
            var builder = new DaprClientBuilder();

            var configuration = serviceProvider.GetService<IConfiguration>();

            //Set the HTTP endpoint, if provided
            var httpEndpoint = GetHttpEndpoint(configuration);
            if (!string.IsNullOrWhiteSpace(httpEndpoint))
                builder.UseHttpEndpoint(httpEndpoint);

            //Set the gRPC endpoint, if provided
            var grpcEndpoint = GetGrpcEndpoint(configuration);
            if (!string.IsNullOrWhiteSpace(grpcEndpoint))
                builder.UseGrpcEndpoint(grpcEndpoint);

            //Set the API token, if provided
            var apiToken = GetApiToken(configuration);
            if (!string.IsNullOrWhiteSpace(apiToken))
                builder.UseDaprApiToken(apiToken);

            configure?.Invoke(builder);

            return builder.Build();
        });
    }

    /// <summary>
    /// Adds Dapr client services to the provided <see cref="IServiceCollection"/>. This does not include integration
    /// with ASP.NET Core MVC. Use the <c>AddDapr()</c> extension method on <c>IMvcBuilder</c> to register MVC integration. 
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configure"></param>
    public static void AddDaprClient(this IServiceCollection services,
        Action<IServiceProvider, DaprClientBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        services.TryAddSingleton(serviceProvider =>
        {
            var builder = new DaprClientBuilder();

            var configuration = serviceProvider.GetService<IConfiguration>();

            //Set the HTTP endpoint, if provided
            var httpEndpoint = GetHttpEndpoint(configuration);
            if (!string.IsNullOrWhiteSpace(httpEndpoint))
                builder.UseHttpEndpoint(httpEndpoint);

            //Set the gRPC endpoint, if provided
            var grpcEndpoint = GetGrpcEndpoint(configuration);
            if (!string.IsNullOrWhiteSpace(grpcEndpoint))
                builder.UseGrpcEndpoint(grpcEndpoint);

            //Set the API token, if provided
            var apiToken = GetApiToken(configuration);
            if (!string.IsNullOrWhiteSpace(apiToken))
                builder.UseDaprApiToken(apiToken);

            configure?.Invoke(serviceProvider, builder);

            return builder.Build();
        });
    }

    /// <summary>
    /// Builds the Dapr HTTP endpoint using the value from the IConfiguration, if available, then falling back
    /// to the value in the environment variable(s) and finally otherwise using the default value (an empty string).
    /// </summary>
    /// <remarks>
    /// Marked as internal for testing purposes.
    /// </remarks>
    /// <param name="configuration">An injected instance of the <see cref="IConfiguration"/>.</param>
    /// <returns>The built HTTP endpoint.</returns>
    internal static string GetHttpEndpoint(IConfiguration? configuration)
    {
        //Prioritize pulling from IConfiguration with a fallback of pulling from the environment variable directly
        var httpEndpoint = GetResourceValue(configuration, DaprDefaults.DaprHttpEndpointName);
        var httpPort = GetResourceValue(configuration, DaprDefaults.DaprHttpPortName);
        int? parsedHttpPort = int.TryParse(httpPort, out var port) ? port : null;

        var endpoint = BuildEndpoint(httpEndpoint, parsedHttpPort);
        return string.IsNullOrWhiteSpace(endpoint)
            ? $"http://{DaprDefaults.DaprHostName}:{DaprDefaults.DefaultHttpPort}/"
            : endpoint;
    }

    /// <summary>
    /// Builds the Dapr gRPC endpoint using the value from the IConfiguration, if available, then falling back
    /// to the value in the environment variable(s) and finally otherwise using the default value (an empty string).
    /// </summary>
    /// <remarks>
    /// Marked as internal for testing purposes.
    /// </remarks>
    /// <param name="configuration">An injected instance of the <see cref="IConfiguration"/>.</param>
    /// <returns>The built gRPC endpoint.</returns>
    internal static string GetGrpcEndpoint(IConfiguration? configuration)
    {
        //Prioritize pulling from IConfiguration with a fallback from pulling from the environment variable directly
        var grpcEndpoint = GetResourceValue(configuration, DaprDefaults.DaprGrpcEndpointName);
        var grpcPort = GetResourceValue(configuration, DaprDefaults.DaprGrpcPortName);
        int? parsedGrpcPort = int.TryParse(grpcPort, out var port) ? port : null;

        var endpoint = BuildEndpoint(grpcEndpoint, parsedGrpcPort);
        return string.IsNullOrWhiteSpace(endpoint)
            ? $"http://{DaprDefaults.DaprHostName}:{DaprDefaults.DefaultGrpcPort}/"
            : endpoint;
    }

    /// <summary>
    /// Retrieves the Dapr API token first from the <see cref="IConfiguration"/>, if available, then falling back
    /// to the value in the environment variable(s) directly, then finally otherwise using the default value (an
    /// empty string).
    /// </summary>
    /// <remarks>
    /// Marked as internal for testing purposes.
    /// </remarks>
    /// <param name="configuration">An injected instance of the <see cref="IConfiguration"/>.</param>
    /// <returns>The Dapr API token.</returns>
    internal static string GetApiToken(IConfiguration? configuration)
    {
        //Prioritize pulling from IConfiguration with a fallback of pulling from the environment variable directly
        return GetResourceValue(configuration, DaprDefaults.DaprApiTokenName);
    }

    /// <summary>
    /// Retrieves the specified value prioritizing pulling it from <see cref="IConfiguration"/>, falling back
    /// to an environment variable, and using an empty string as a default.
    /// </summary>
    /// <param name="configuration">An instance of an <see cref="IConfiguration"/>.</param>
    /// <param name="name">The name of the value to retrieve.</param>
    /// <returns>The value of the resource.</returns>
    private static string GetResourceValue(IConfiguration? configuration, string name)
    {
        //Attempt to retrieve first from the configuration
        var configurationValue = configuration?.GetValue<string?>(name);
        if (configurationValue is not null)
            return configurationValue;

        //Fall back to the environment variable with the same name or default to an empty string
        var envVar = Environment.GetEnvironmentVariable(name);
        return envVar ?? string.Empty;
    }

    /// <summary>
    /// Builds the endpoint provided an optional endpoint and optional port.
    /// </summary>
    /// <remarks>
    /// Marked as internal for testing purposes.
    /// </remarks>
    /// <param name="endpoint">The endpoint.</param>
    /// <param name="endpointPort">The port</param>
    /// <returns>A constructed endpoint value.</returns>
    internal static string BuildEndpoint(string? endpoint, int? endpointPort)
    {
        if (string.IsNullOrWhiteSpace(endpoint) && endpointPort is null)
            return string.Empty;
        
        // Per the proposal at https://github.com/artursouza/proposals/blob/cd811136d0af0aade52ef297a84c3050f3243ae8/0008-S-sidecar-endpoint-tls.md#design this will
        // favor whatever value is provided in the endpoint value (and if the port isn't provided, it will be inferred from the protocol).
        
        // The endpoint port will only be evaluated if the endpoint is not provided. While the proposal calls for a value of "127.0.0.1", it does accept its
        // equivalent, e.g. "localhost", and because of the issue detailed at https://github.com/dapr/dotnet-sdk/issues/1032
        
        var endpointBuilder = new UriBuilder();
        if (!string.IsNullOrWhiteSpace(endpoint) && endpointPort is null)
        {
            //Extract the scheme, host and port from the endpoint
            var uri = new Uri(endpoint);
            endpointBuilder.Scheme = uri.Scheme;
            endpointBuilder.Host = uri.Host;
            endpointBuilder.Port = uri.Port;
        }
        else if (string.IsNullOrWhiteSpace(endpoint) && endpointPort is not null)
        {
            endpointBuilder.Host = "localhost";
            endpointBuilder.Port = (int)endpointPort;
        }

        return endpointBuilder.ToString();
    }
}
