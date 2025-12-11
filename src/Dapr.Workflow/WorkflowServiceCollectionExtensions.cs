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
    /// Adds Dapr Workflow support to the service collection.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configure">A delegate used to configure workflow options and register workflow functions.</param>
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
        
        // Register default JSON serializer if no custom serializer is registered
        serviceCollection.TryAddSingleton<IWorkflowSerializer>(
            new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        
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

    /// <summary>
    /// Configures a custom workflow serializer to replace the default JSON serializer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call this method <b>before</b> <see cref="AddDaprWorkflow"/> for cleaner intent.
    /// However, it can also be called <b>after</b> if needed, and will replace the default serializer.
    /// </para>
    /// <para>
    /// By default, workflows use <see cref="JsonWorkflowSerializer"/> with <see cref="JsonSerializerDefaults.Web"/> settings.
    /// Use this method to provide alternative serialization (e.g., MessagePack, Protobuf, or custom JSON settings).
    /// </para>
    /// <para>
    /// <b>Warning:</b> Changing serializers between application deployments will cause deserialization errors
    /// for in-flight workflows due to format incompatibilities.
    /// </para>
    /// </remarks>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="serializer">The custom serializer instance to use.</param>
    /// <returns>The <see cref="IServiceCollection"/> for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="services"/> or <paramref name="serializer"/> is null.</exception>
    public static IServiceCollection AddDaprWorkflowSerializer(this IServiceCollection services,
        IWorkflowSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serializer);
        
        // Replace any existing registration
        services.AddSingleton(serializer);

        return services;
    }
    
    /// <summary>
    /// Configures a custom workflow serializer using a factory method.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call this method <b>before</b> <see cref="AddDaprWorkflow"/> for cleaner intent.
    /// However, it can also be called <b>after</b> if needed, and will replace the default serializer.
    /// </para>
    /// <para>
    /// Use this overload when your serializer needs dependencies from the service provider.
    /// </para>
    /// </remarks>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="serializerFactory">A factory function that creates the serializer using the service provider.</param>
    /// <returns>The <see cref="IServiceCollection"/> for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="services"/> or <paramref name="serializerFactory"/> is null.</exception>
    public static IServiceCollection AddDaprWorkflowSerializer(
        this IServiceCollection services,
        Func<IServiceProvider, IWorkflowSerializer> serializerFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serializerFactory);

        // Replace any existing registration
        services.AddSingleton(serializerFactory);
        
        return services;
    }

    /// <summary>
    /// Configures the default JSON workflow serializer with custom options.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call this method <b>before</b> <see cref="AddDaprWorkflow"/> for cleaner intent.
    /// However, it can also be called <b>after</b> if needed, and will replace the default serializer.
    /// </para>
    /// <para>
    /// Use this as a convenient alternative to <see cref="AddDaprWorkflowSerializer(IServiceCollection, IWorkflowSerializer)"/>
    /// when you want to customize JSON serialization without implementing a custom serializer.
    /// </para>
    /// </remarks>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="jsonOptions">The JSON serializer options to use.</param>
    /// <returns>The <see cref="IServiceCollection"/> for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="services"/> or <paramref name="jsonOptions"/> is null.</exception>
    public static IServiceCollection AddDaprWorkflowJsonSerializer(
        this IServiceCollection services,
        JsonSerializerOptions jsonOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(jsonOptions);

        return services.AddDaprWorkflowSerializer(new JsonWorkflowSerializer(jsonOptions));
    }
}
