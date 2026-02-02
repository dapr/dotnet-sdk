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
//  ------------------------------------------------------------------------

using System;
using System.Text.Json;
using Dapr.Common;
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Client;
using Dapr.Workflow.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dapr.Workflow.Registration;

/// <summary>
/// Fluent builder for optional workflow configuration (e.g. serialization registration).
/// </summary>
public sealed class DaprWorkflowClientBuilder(IConfiguration? configuration = null) : DaprGenericClientBuilder<DaprWorkflowClient>(configuration)
{
    private IWorkflowSerializer? _serializer;
    private IServiceProvider? _serviceProvider;
    private Func<IServiceProvider, IWorkflowSerializer>? _serializerFactory;
    
    /// <summary>
    /// Configures a custom workflow serializer to replace the default JSON serializer.
    /// </summary>
    /// <param name="serializer">The custom serializer instance to use.</param>
    /// <returns>The <see cref="DaprWorkflowClientBuilder" /> instance.</returns>
    public DaprWorkflowClientBuilder UseSerializer(IWorkflowSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        _serializer = serializer;
        _serializerFactory = null;
        return this;
    }
    
    /// <summary>
    /// Configures a custom workflow serializer using a factory method.
    /// </summary>
    /// <param name="serializerFactory">A factory function that creates the serializer using the service provider.</param>
    /// <returns>The <see cref="DaprWorkflowClientBuilder" /> instance.</returns>
    public DaprWorkflowClientBuilder UseSerializer(Func<IServiceProvider, IWorkflowSerializer> serializerFactory)
    {
        ArgumentNullException.ThrowIfNull(serializerFactory);
        _serializerFactory = serializerFactory;
        _serializer = null;
        return this;
    }
    
    /// <summary>
    /// Configures the default System.Text.Json serializer with custom options.
    /// </summary>
    /// <param name="jsonOptions">The JSON serializer options to use.</param>
    /// <returns>The <see cref="DaprWorkflowClientBuilder" /> instance.</returns>
    public DaprWorkflowClientBuilder UseJsonSerializer(JsonSerializerOptions jsonOptions)
    {
        ArgumentNullException.ThrowIfNull(jsonOptions);
        return UseSerializer(new JsonWorkflowSerializer(jsonOptions));
    }

    /// <summary>
    /// Internal method used to set the service provider for factory resolution.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    internal DaprWorkflowClientBuilder UseServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        return this;
    }

    /// <inheritdoc />
    public override DaprWorkflowClient Build()
    {
        // Try to get gRPC client from DI first (ensures custom configuration is honored)
        TaskHubSidecarService.TaskHubSidecarServiceClient grpcClient;
        if (_serviceProvider != null)
        {
            var diGrpcClient = _serviceProvider.GetService<TaskHubSidecarService.TaskHubSidecarServiceClient>();
            if (diGrpcClient != null)
            {
                grpcClient = diGrpcClient;
            }
            else
            {
                // Fallback: create new gRPC client (not recommended for Workflow)
                var (channel, _, _, _) = BuildDaprClientDependencies(typeof(DaprWorkflowClient).Assembly);
                grpcClient = new TaskHubSidecarService.TaskHubSidecarServiceClient(channel);
            }
        }
        else
        {
            // No service provider - create new gRPC client
            var (channel, _, _, _) = BuildDaprClientDependencies(typeof(DaprWorkflowClient).Assembly);
            grpcClient = new TaskHubSidecarService.TaskHubSidecarServiceClient(channel);
        }
        
        // Resolve serializer
        IWorkflowSerializer serializer;
        if (_serializer is not null)
        {
            serializer = _serializer;
        }
        else
        {
            serializer = _serializerFactory is not null && _serviceProvider is not null
                ? _serializerFactory(_serviceProvider)
                : new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        
        // Resolve logger
        ILogger<WorkflowGrpcClient> logger = NullLogger<WorkflowGrpcClient>.Instance;
        var loggerFactory = _serviceProvider?.GetService<ILoggerFactory>();
        if (loggerFactory != null)
        {
            logger = loggerFactory.CreateLogger<WorkflowGrpcClient>();
        }

        var innerClient = new WorkflowGrpcClient(grpcClient, logger, serializer);
        return new DaprWorkflowClient(innerClient);
    }
}
