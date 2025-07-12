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

namespace Dapr.Workflow;

/// <summary>
/// A factory for building a <see cref="DaprWorkflowClient"/>.
/// </summary>
internal sealed class DaprWorkflowClientBuilderFactory(IConfiguration? configuration, IHttpClientFactory httpClientFactory)
{
    /// <summary>
    /// Responsible for building the client itself.
    /// </summary>
    /// <returns></returns>
    public void CreateClientBuilder(IServiceCollection services, Action<WorkflowRuntimeOptions> configure)
    {
        services.AddDurableTaskClient(builder =>
        {
            WorkflowRuntimeOptions options = new();
            configure.Invoke(options);

            var apiToken = DaprDefaults.GetDefaultDaprApiToken(configuration);
            var grpcEndpoint = DaprDefaults.GetDefaultGrpcEndpoint(configuration);

            var httpClient = httpClientFactory.CreateClient();

            if (!string.IsNullOrWhiteSpace(apiToken))
            {
                httpClient.DefaultRequestHeaders.Add("Dapr-Api-Token", apiToken);
            }

            var channelOptions = options.GrpcChannelOptions ?? new GrpcChannelOptions
            {
                HttpClient = httpClient
            };

            builder.UseGrpc(GrpcChannel.ForAddress(grpcEndpoint, channelOptions));
            builder.RegisterDirectly();
        });

        services.AddDurableTaskWorker(builder =>
        {
            WorkflowRuntimeOptions options = new();
            configure.Invoke(options);

            var apiToken = DaprDefaults.GetDefaultDaprApiToken(configuration);
            var grpcEndpoint = DaprDefaults.GetDefaultGrpcEndpoint(configuration);

            if (!string.IsNullOrEmpty(grpcEndpoint))
            {
                var httpClient = httpClientFactory.CreateClient();

                if (!string.IsNullOrWhiteSpace(apiToken))
                {
                    httpClient.DefaultRequestHeaders.Add("Dapr-Api-Token", apiToken);
                }

                var channelOptions = options.GrpcChannelOptions ?? new GrpcChannelOptions
                {
                    HttpClient = httpClient
                };

                builder.UseGrpc(GrpcChannel.ForAddress(grpcEndpoint, channelOptions));
            }
            else
            {
                builder.UseGrpc();
            }

            builder.AddTasks(registry => options.AddWorkflowsAndActivitiesToRegistry(registry));
        });
    }
}
