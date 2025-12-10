// ------------------------------------------------------------------------
// Copyright 2022 The Dapr Authors
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
using Dapr.Workflow.Grpc.Extensions;
using Dapr.Workflow.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Dapr.Workflow;

/// <summary>
/// Contains extension methods for using Dapr Workflow with dependency injection.
/// </summary>
public static class WorkflowServiceCollectionExtensions
{
    /// <summary>
    /// Adds Dapr Workflow support to the service collection sourcing options from the configuration under the key "DaprWorkflow". 
    /// </summary>
    /// <example>
    /// Configuration is read from the "DaprWorkflow" section. Example:
    /// <code>
    /// {
    ///   "DaprWorkflow": {
    ///     "MaxConcurrentWorkflows" 100,
    ///     "MaxConcurrentActivities": "100
    ///   }
    /// }
    /// </code>
    /// </example>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configuration">The applications's configuration.</param>
    /// <param name="lifetime">The lifetime of the registered service.</param>
    /// <returns></returns>
    public static IServiceCollection AddDaprWorkflow(
        this IServiceCollection serviceCollection,
        IConfiguration configuration,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(serviceCollection);
        ArgumentNullException.ThrowIfNull(configuration);
        
        return serviceCollection.AddDaprWorkflow(opts =>
        {
            configuration.GetSection("DaprWorkflow").Bind(opts);
        }, lifetime);
    }

    /// <summary>
    /// Adds Dapr Workflow support to the service collection.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configure">A delegate used to configure actor options and register workflow functions.</param>
    /// <param name="lifetime">The lifetime of the registered services.</param>
    public static IServiceCollection AddDaprWorkflow(
        this IServiceCollection serviceCollection,
        Action<WorkflowRuntimeOptions> configure,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(serviceCollection);
        
        // Configure workflow runtime options
        var options = new WorkflowRuntimeOptions();
        configure(options);
        
        // Register options as a singleton as they don't change a runtime
        serviceCollection.AddSingleton(options);
        
        // Register the workflow factory
        serviceCollection.TryAddSingleton<WorkflowsFactory>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<WorkflowsFactory>();
            var factory = new WorkflowsFactory(logger);
            
            // Apply all registrations from options
            options.ApplyRegistrations(factory);

            return factory;
        });

        // Necessary for the gRPC client factory
        serviceCollection.AddHttpClient();
        
        // Register the IWorkflowsFactory interface
        serviceCollection.TryAddSingleton<IWorkflowsFactory>(sp => sp.GetRequiredService<WorkflowsFactory>());
        
        // Register gRPC client for communicating with Dapr sidecar
        // This sets up a proper long-lived streaming connection configuration
        serviceCollection.AddDaprWorkflowGrpcClient(grpcOptions =>
        {
            // Apply custom gRPC channel options if configured
            if (options.GrpcChannelOptions != null)
            {
                grpcOptions.ChannelOptionsActions.Add(channelOptions =>
                {
                    // Copy over any custom settings from options
                    if (options.GrpcChannelOptions.MaxReceiveMessageSize.HasValue)
                    {
                        channelOptions.MaxReceiveMessageSize = options.GrpcChannelOptions.MaxReceiveMessageSize;
                    }

                    if (options.GrpcChannelOptions.MaxSendMessageSize.HasValue)
                    {
                        channelOptions.MaxSendMessageSize = options.GrpcChannelOptions.MaxSendMessageSize;
                    }
                });
            }
        });
        
        // Register the workflow worker as a hosted service
        serviceCollection.AddHostedService<WorkflowWorker>();
        
        // Register the workflow client with the specified lifetime
        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                serviceCollection.TryAddSingleton<DaprWorkflowClient>();
                break;
            case ServiceLifetime.Scoped:
                serviceCollection.TryAddScoped<DaprWorkflowClient>();
                break;
            case ServiceLifetime.Transient:
                serviceCollection.TryAddTransient<DaprWorkflowClient>();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, @"Invalid service lifetime");
        }

        return serviceCollection;
    }
}
