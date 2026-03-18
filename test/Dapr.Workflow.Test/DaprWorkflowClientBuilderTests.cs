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

using System.Reflection;
using System.Text.Json;
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Client;
using Dapr.Workflow.Registration;
using Dapr.Workflow.Serialization;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Dapr.Workflow.Test;

public class DaprWorkflowClientBuilderTests
{
    [Fact]
    public void Build_ShouldUseServiceProviderGrpcClientLoggerAndSerializer()
    {
        var grpcClient = CreateGrpcClient();
        var logger = new TestLogger();

        var services = new ServiceCollection();
        services.AddSingleton(grpcClient);
        services.AddSingleton<ILoggerFactory>(new StubLoggerFactory(logger));

        var provider = services.BuildServiceProvider();

        var serializer = new TrackingSerializer("custom");
        var builder = new DaprWorkflowClientBuilder()
            .UseServiceProvider(provider)
            .UseSerializer(serializer);

        var client = builder.Build();

        var inner = GetInnerClient(client);
        var usedGrpcClient = GetPrivateField<TaskHubSidecarService.TaskHubSidecarServiceClient>(inner);
        var usedLogger = GetPrivateField<ILogger<WorkflowGrpcClient>>(inner);
        var innerLogger = UnwrapLogger(usedLogger);
        var usedSerializer = GetPrivateField<IWorkflowSerializer>(inner);

        Assert.Same(grpcClient, usedGrpcClient);
        Assert.Same(logger, innerLogger);
        Assert.Same(serializer, usedSerializer);
    }

    [Fact]
    public void Build_ShouldUseSerializerFactory_WhenConfigured()
    {
        var grpcClient = CreateGrpcClient();
        var dependency = new SerializerDependency("dep");

        var services = new ServiceCollection();
        services.AddSingleton(grpcClient);
        services.AddSingleton(dependency);
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

        var provider = services.BuildServiceProvider();

        SerializerDependency? seenDependency = null;
        DependencySerializer? createdSerializer = null;

        var builder = new DaprWorkflowClientBuilder()
            .UseServiceProvider(provider)
            .UseSerializer(sp =>
            {
                seenDependency = sp.GetRequiredService<SerializerDependency>();
                createdSerializer = new DependencySerializer(seenDependency);
                return createdSerializer;
            });

        var client = builder.Build();
        var inner = GetInnerClient(client);
        var usedSerializer = GetPrivateField<IWorkflowSerializer>(inner);

        Assert.Same(dependency, seenDependency);
        Assert.Same(createdSerializer, usedSerializer);
    }

    [Fact]
    public void Build_ShouldUseDefaultJsonSerializer_WhenNoSerializerConfigured()
    {
        var builder = new DaprWorkflowClientBuilder();

        var client = builder.Build();
        var inner = GetInnerClient(client);
        var serializer = GetPrivateField<IWorkflowSerializer>(inner);

        var json = Assert.IsType<JsonWorkflowSerializer>(serializer);
        var payload = json.Serialize(new { MyValue = 1 });

        Assert.Contains("\"myValue\"", payload);
    }

    [Fact]
    public void Build_ShouldUseJsonSerializerOptions_WhenConfigured()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = null };
        var builder = new DaprWorkflowClientBuilder().UseJsonSerializer(options);

        var client = builder.Build();
        var inner = GetInnerClient(client);
        var serializer = GetPrivateField<IWorkflowSerializer>(inner);

        var json = Assert.IsType<JsonWorkflowSerializer>(serializer);
        var payload = json.Serialize(new { MyValue = 1 });

        Assert.Contains("\"MyValue\"", payload);
    }

    [Fact]
    public void Build_ShouldFallbackToNewGrpcClient_WhenServiceProviderHasNoGrpcClient()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

        var provider = services.BuildServiceProvider();

        var builder = new DaprWorkflowClientBuilder().UseServiceProvider(provider);

        var client = builder.Build();
        var inner = GetInnerClient(client);
        var grpcClient = GetPrivateField<TaskHubSidecarService.TaskHubSidecarServiceClient>(inner);

        Assert.NotNull(grpcClient);
    }

    private static TaskHubSidecarService.TaskHubSidecarServiceClient CreateGrpcClient()
    {
        var callInvoker = new Mock<CallInvoker>(MockBehavior.Loose);
        return new TaskHubSidecarService.TaskHubSidecarServiceClient(callInvoker.Object);
    }

    private static WorkflowClient GetInnerClient(DaprWorkflowClient client)
    {
        var field = typeof(DaprWorkflowClient).GetField("_innerClient", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        return (WorkflowClient)field.GetValue(client)!;
    }

    private static T GetPrivateField<T>(object instance)
    {
        var field = instance
            .GetType()
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(f => typeof(T).IsAssignableFrom(f.FieldType));

        Assert.NotNull(field);
        var value = field.GetValue(instance);
        Assert.NotNull(value);
        return (T)value;
    }

    private static ILogger UnwrapLogger(ILogger logger)
    {
        var field = logger
            .GetType()
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(f => typeof(ILogger).IsAssignableFrom(f.FieldType));

        return field?.GetValue(logger) as ILogger ?? logger;
    }

    private sealed class TrackingSerializer(string name) : IWorkflowSerializer
    {
        private string Name { get; } = name;

        public string Serialize(object? value, Type? inputType = null) => Name;
        public T? Deserialize<T>(string? data) => default;
        public object? Deserialize(string? data, Type returnType) => null;
    }

    private sealed record SerializerDependency(string Value);

    private sealed class DependencySerializer(SerializerDependency dep) : IWorkflowSerializer
    {
        private SerializerDependency Dependency { get; } = dep;

        public string Serialize(object? value, Type? inputType = null) => Dependency.Value;
        public T? Deserialize<T>(string? data) => default;
        public object? Deserialize(string? data, Type returnType) => null;
    }

    private sealed class TestLogger : ILogger<WorkflowGrpcClient>
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }

    private sealed class StubLoggerFactory(ILogger logger) : ILoggerFactory
    {
        private ILogger Logger { get; } = logger;

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => Logger;

        public void Dispose()
        {
        }
    }

    private sealed class NoopScope : IDisposable
    {
        public static NoopScope Instance { get; } = new();
        public void Dispose()
        {
        }
    }
}
