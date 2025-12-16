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
using System.Text.Json;
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Client;
using Dapr.Workflow.Grpc.Extensions;
using Dapr.Workflow.Serialization;
using Dapr.Workflow.Worker;
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
    /// Fluent builder for optional workflow configuration (e.g. serialization registration).
    /// </summary>
    public readonly struct DaprWorkflowBuilder
    {
        internal DaprWorkflowBuilder(IServiceCollection services) => Services = services;
        
        /// <summary>
        /// Provides the services in the DI container collection.
        /// </summary>
        public IServiceCollection Services { get; }

        /// <summary>
        /// Configures a custom workflow serialzier to replace the default JSON serializer. 
        /// </summary>
        /// <param name="serializer">The custom serializer instance to use.</param>
        public DaprWorkflowBuilder WithSerializer(IWorkflowSerializer serializer)
        {
            ArgumentNullException.ThrowIfNull(serializer);
            Services.Replace(ServiceDescriptor.Singleton(typeof(IWorkflowSerializer), serializer));
            return this;
        }

        /// <summary>
        /// Configures a custom workflow serializer using a factory method.
        /// </summary>
        /// <param name="serializerFactory">A factory function that creates the serializer using the service provider.</param>
        public DaprWorkflowBuilder WithSerializer(Func<IServiceProvider, IWorkflowSerializer> serializerFactory)
        {
            ArgumentNullException.ThrowIfNull(serializerFactory);

            Services.Replace(ServiceDescriptor.Singleton(typeof(IWorkflowSerializer), serializerFactory));
            return this;
        }

        /// <summary>
        /// Configures the default System.Text.Json serialzier with custom options. 
        /// </summary>
        /// <param name="jsonOptions">The JSON serialzier options to use.</param>
        public DaprWorkflowBuilder WithJsonSerializer(JsonSerializerOptions jsonOptions)
        {
            ArgumentNullException.ThrowIfNull(jsonOptions);
            return WithSerializer(new JsonWorkflowSerializer(jsonOptions));
        }
    }

    /// <summary>
    /// Adds Dapr Workflow support with defaults.
    /// </summary>
    public static IServiceCollection AddDaprWorkflow(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        AddDaprWorkflowCore(services, _ => { }, ServiceLifetime.Singleton);
        return services;
    }

    /// <summary>
    /// Adds Dapr Workflow support to the service collection.
    /// </summary>
    public static IServiceCollection AddDaprWorkflow(this IServiceCollection services, Action<WorkflowRuntimeOptions> configure, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        AddDaprWorkflowCore(services, configure, lifetime);
        return services;
    }

    /// <summary>
    /// Adds Dapr Workflow to the dependency injection container and retursn a builder for additional optional configuration.
    /// </summary>
    public static DaprWorkflowBuilder AddDaprWorkflowBuilder(this IServiceCollection services,
        Action<WorkflowRuntimeOptions>? configure = null, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(services);

        AddDaprWorkflowCore(services, configure ?? (_ => { }), lifetime);
        return new DaprWorkflowBuilder(services);
    }

    private static void AddDaprWorkflowCore(IServiceCollection serviceCollection,
        Action<WorkflowRuntimeOptions> configure, ServiceLifetime lifetime)
    {
        // Configure workflow runtime options
        var options = new WorkflowRuntimeOptions();
        configure(options);

        // Register options as a singleton as they don't change a runtime
        serviceCollection.AddSingleton(options);

        // Register default JSON serializer if no custom serializer is registered
        serviceCollection.TryAddSingleton<IWorkflowSerializer>(
            new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        // Register the workflow factory
        serviceCollection.TryAddSingleton<IWorkflowsFactory>(sp =>
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

        // Register the internal WorkflowClient implementation
        serviceCollection.TryAddSingleton<WorkflowClient>(sp =>
        {
            var grpcClient = sp.GetRequiredService<TaskHubSidecarService.TaskHubSidecarServiceClient>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<WorkflowGrpcClient>();
            var serializer = sp.GetRequiredService<IWorkflowSerializer>();
            return new WorkflowGrpcClient(grpcClient, logger, serializer);
        });

        // Register gRPC client for communicating with Dapr sidecar
        serviceCollection.AddDaprWorkflowGrpcClient(grpcOptions =>
        {
            if (options.GrpcChannelOptions != null)
            {
                grpcOptions.ChannelOptionsActions.Add(channelOptions =>
                {
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
                serviceCollection.TryAddSingleton<DaprWorkflowClient>(sp =>
                {
                    var inner = sp.GetRequiredService<WorkflowClient>();
                    return new DaprWorkflowClient(inner);
                });
                break;
            case ServiceLifetime.Scoped:
                serviceCollection.TryAddScoped<DaprWorkflowClient>(sp =>
                {
                    var inner = sp.GetRequiredService<WorkflowClient>();
                    return new DaprWorkflowClient(inner);
                });
                break;
            case ServiceLifetime.Transient:
                serviceCollection.TryAddTransient<DaprWorkflowClient>(sp =>
                {
                    var inner = sp.GetRequiredService<WorkflowClient>();
                    return new DaprWorkflowClient(inner);
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Invalid service lifetime");
        }
    }
}
