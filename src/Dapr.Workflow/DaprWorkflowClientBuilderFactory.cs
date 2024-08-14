// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

using System;
using System.Net.Http;
using Grpc.Net.Client;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

#nullable enable

namespace Dapr.Workflow;

/// <summary>
/// A factory for building a <see cref="DaprWorkflowClient"/>.
/// </summary>
internal sealed class DaprWorkflowClientBuilderFactory
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceCollection _services;
    
    /// <summary>
    /// Constructor used to inject the required types into the factory.
    /// </summary>
    public DaprWorkflowClientBuilderFactory(IConfiguration configuration, IHttpClientFactory httpClientFactory, IServiceCollection services)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _services = services;
    }
    
    /// <summary>
    /// Responsible for building the client itself.
    /// </summary>
    /// <returns></returns>
    public void CreateClientBuilder(Action<WorkflowRuntimeOptions> configure)
    {
        _services.AddDurableTaskClient(builder =>
        {
            var apiToken = GetApiToken(_configuration);
            var grpcEndpoint = GetGrpcEndpoint(_configuration);

            var httpClient = _httpClientFactory.CreateClient();

            if (!string.IsNullOrWhiteSpace(apiToken))
            {
                httpClient.DefaultRequestHeaders.Add("Dapr-Api-Token", apiToken);    
            }

            builder.UseGrpc(GrpcChannel.ForAddress(grpcEndpoint, new GrpcChannelOptions { HttpClient = httpClient }));
            builder.RegisterDirectly();
        });

        _services.AddDurableTaskWorker(builder =>
        {
            WorkflowRuntimeOptions options = new();
            configure?.Invoke(options);

            var apiToken = GetApiToken(_configuration);
            var grpcEndpoint = GetGrpcEndpoint(_configuration);

            if (!string.IsNullOrEmpty(grpcEndpoint))
            {
                var httpClient = _httpClientFactory.CreateClient();

                if (!string.IsNullOrWhiteSpace(apiToken))
                {
                    httpClient.DefaultRequestHeaders.Add("Dapr-Api-Token", apiToken);
                }

                builder.UseGrpc(
                    GrpcChannel.ForAddress(grpcEndpoint, new GrpcChannelOptions { HttpClient = httpClient }));
            }
            else
            {
                builder.UseGrpc();
            }

            builder.AddTasks(registry => options.AddWorkflowsAndActivitiesToRegistry(registry));
        });
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
    private string GetGrpcEndpoint(IConfiguration? configuration)
    {
        //Prioritize pulling from IConfiguration with a fallback from pulling from the environment variable directly
        var grpcEndpoint = GetResourceValue(configuration, DaprDefaults.DaprGrpcEndpointName);
        var grpcPort = GetResourceValue(configuration, DaprDefaults.DaprGrpcPortName);
        int? parsedGrpcPort = string.IsNullOrWhiteSpace(grpcPort) ? null : int.Parse(grpcPort);

        var endpoint = BuildEndpoint(grpcEndpoint, parsedGrpcPort);
        return string.IsNullOrWhiteSpace(endpoint) ? $"http://localhost:{DaprDefaults.DefaultGrpcPort}/" : endpoint;
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
    private string GetApiToken(IConfiguration? configuration)
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

        var endpointBuilder = new UriBuilder();
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            //Extract the scheme, host and port from the endpoint
            var uri = new Uri(endpoint);
            endpointBuilder.Scheme = uri.Scheme;
            endpointBuilder.Host = uri.Host;
            endpointBuilder.Port = uri.Port;

            //Update the port if provided separately
            if (endpointPort is not null)
                endpointBuilder.Port = (int)endpointPort;
        }
        else if (string.IsNullOrWhiteSpace(endpoint) && endpointPort is not null)
        {
            endpointBuilder.Host = "localhost";
            endpointBuilder.Port = (int)endpointPort;
        }

        return endpointBuilder.ToString();
    }
}
