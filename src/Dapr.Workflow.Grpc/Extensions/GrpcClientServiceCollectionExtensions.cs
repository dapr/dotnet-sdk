// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

using Dapr.DurableTask.Protobuf;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Workflow.Grpc.Extensions;

/// <summary>
/// Extension methods for registering Dapr Workflow gRPC clients with <see cref="IHttpClientFactory"/>.
/// </summary>
public static class GrpcClientServiceCollectionExtensions
{
    public static IHttpClientBuilder AddDaprWorkflowGrpcClient(
        this IServiceCollection services,
        Action<GrpcClientFactoryOptions>? configureClient = null) =>
        services.AddGrpcClient<TaskHubSidecarService.TaskHubSidecarServiceClient>(options =>
            {
                // Default to Dapr sidecar address
                options.Address = new Uri(GetDaprGrpcAddress());

                // Configure for long-lived streaming connections
                options.ChannelOptionsActions.Add(channelOptions =>
                {
                    // Disable idle timeout - connection should never timeout due to inactivity
                    channelOptions.HttpHandler = new SocketsHttpHandler
                    {
                        // Disable all timeouts for long-lived connections
                        PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
                        KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                        KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                        EnableMultipleHttp2Connections = true,

                        // Ensure connections are kept alive
                        KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always
                    };

                    // Configure channel-level settings for resilience
                    channelOptions.MaxReceiveMessageSize = null; // No size limit
                    channelOptions.MaxSendMessageSize = null; // No size limit
                });

                // Allow consumer to override
                configureClient?.Invoke(options);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                // Infinite timeouts - connection should never timeout
                ConnectTimeout = Timeout.InfiniteTimeSpan,
                PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
                PooledConnectionLifetime = Timeout.InfiniteTimeSpan,

                // HTTP/2 keep-alive settings for long-lived connections
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always,

                // Enable multiple HTTP/2 connections for better throughput
                EnableMultipleHttp2Connections = true,
            })
            .ConfigureHttpClient(httpClient =>
            {
                // Disable HttpClient's own timeout - let gRPC handle it
                httpClient.Timeout = Timeout.InfiniteTimeSpan;
            });

    /// <summary>
    /// Gets the Dapr gRPC address from environment variables or uses default.
    /// </summary>
    private static string GetDaprGrpcAddress()
    {
        // TODO Improve to use failover approach with options
        // Check for the Dapr sidecar address environment variable
        var daprGrpcPort = Environment.GetEnvironmentVariable("DAPR_GRPC_PORT") ?? "50001";
        var daprHost = Environment.GetEnvironmentVariable("DAPR_HOST") ?? "localhost";

        return $"http://{daprHost}:{daprGrpcPort}";
    }
}
