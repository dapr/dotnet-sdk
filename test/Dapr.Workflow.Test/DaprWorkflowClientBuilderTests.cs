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
using Dapr.Common.Serialization;
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
    // -------------------------------------------------------------------------
    // UseSerializer(IDaprSerializer) overload
    // -------------------------------------------------------------------------

    [Fact]
    public void UseSerializer_WithIDaprSerializer_SetsSerializer()
    {
        var serializer = new StubDaprSerializer("dapr");
        var builder = new DaprWorkflowClientBuilder().UseSerializer(serializer);

        var usedSerializer = BuildAndGetSerializer(builder);

        Assert.Same(serializer, usedSerializer);
    }

    [Fact]
    public void UseSerializer_WithJsonDaprSerializer_SetsSerializer()
    {
        var serializer = new JsonDaprSerializer();
        var builder = new DaprWorkflowClientBuilder().UseSerializer(serializer);

        var usedSerializer = BuildAndGetSerializer(builder);

        Assert.Same(serializer, usedSerializer);
    }

    [Fact]
    public void UseSerializer_WithIWorkflowSerializer_SetsSerializer()
    {
        // IWorkflowSerializer extends IDaprSerializer — backward compat path
        var serializer = new StubWorkflowSerializer("workflow");
        var builder = new DaprWorkflowClientBuilder().UseSerializer(serializer);

        var usedSerializer = BuildAndGetSerializer(builder);

        Assert.Same(serializer, usedSerializer);
    }

    [Fact]
    public void UseSerializer_WithJsonWorkflowSerializer_SetsSerializer()
    {
        // JsonWorkflowSerializer extends JsonDaprSerializer — backward compat path
#pragma warning disable CS0618
        var serializer = new JsonWorkflowSerializer();
#pragma warning restore CS0618
        var builder = new DaprWorkflowClientBuilder().UseSerializer(serializer);

        var usedSerializer = BuildAndGetSerializer(builder);

        Assert.Same(serializer, usedSerializer);
    }

    [Fact]
    public void UseSerializer_WithNullSerializer_ThrowsArgumentNullException()
    {
        var builder = new DaprWorkflowClientBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.UseSerializer((IDaprSerializer)null!));
    }

    // -------------------------------------------------------------------------
    // UseSerializer(Func<IServiceProvider, IWorkflowSerializer>) overload
    // -------------------------------------------------------------------------

    [Fact]
    public void UseSerializer_WithFactory_ResolvesFromServiceProvider()
    {
        var grpcClient = CreateGrpcClient();
        var dependency = new SerializerDependency("dep");

        var services = new ServiceCollection();
        services.AddSingleton(grpcClient);
        services.AddSingleton(dependency);
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

        var provider = services.BuildServiceProvider();

        SerializerDependency? seenDependency = null;
        StubWorkflowSerializer? createdSerializer = null;

        var builder = new DaprWorkflowClientBuilder()
            .UseServiceProvider(provider)
            .UseSerializer(sp =>
            {
                seenDependency = sp.GetRequiredService<SerializerDependency>();
                createdSerializer = new StubWorkflowSerializer(seenDependency.Value);
                return createdSerializer;
            });

        var usedSerializer = BuildAndGetSerializer(builder);

        Assert.Same(dependency, seenDependency);
        Assert.Same(createdSerializer, usedSerializer);
    }

    [Fact]
    public void UseSerializer_WithFactory_WhenNoServiceProvider_FallsBackToDefaultJsonDaprSerializer()
    {
        // Factory is set but there is no service provider — Build() falls back to JsonDaprSerializer
        var builder = new DaprWorkflowClientBuilder()
            .UseSerializer(_ => new StubWorkflowSerializer("unused"));

        var usedSerializer = BuildAndGetSerializer(builder);

        Assert.IsType<JsonDaprSerializer>(usedSerializer);
    }

    [Fact]
    public void UseSerializer_WithNullFactory_ThrowsArgumentNullException()
    {
        var builder = new DaprWorkflowClientBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.UseSerializer((Func<IServiceProvider, IWorkflowSerializer>)null!));
    }

    // -------------------------------------------------------------------------
    // UseJsonSerializer(JsonSerializerOptions) overload
    // -------------------------------------------------------------------------

    [Fact]
    public void UseJsonSerializer_CreatesJsonDaprSerializer()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var builder = new DaprWorkflowClientBuilder().UseJsonSerializer(options);

        var usedSerializer = BuildAndGetSerializer(builder);

        Assert.IsType<JsonDaprSerializer>(usedSerializer);
    }

    [Fact]
    public void UseJsonSerializer_RespectsProvidedNamingPolicy()
    {
        // Default web options use camelCase; null policy preserves PascalCase
        var options = new JsonSerializerOptions { PropertyNamingPolicy = null };
        var builder = new DaprWorkflowClientBuilder().UseJsonSerializer(options);

        var serializer = BuildAndGetSerializer(builder);

        var payload = serializer.Serialize(new { MyValue = 1 });
        Assert.Contains("\"MyValue\"", payload);
        Assert.DoesNotContain("\"myValue\"", payload);
    }

    [Fact]
    public void UseJsonSerializer_WithNullOptions_ThrowsArgumentNullException()
    {
        var builder = new DaprWorkflowClientBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.UseJsonSerializer(null!));
    }

    // -------------------------------------------------------------------------
    // Build() default behavior
    // -------------------------------------------------------------------------

    [Fact]
    public void Build_WithNoSerializer_UsesJsonDaprSerializerWithWebDefaults()
    {
        var builder = new DaprWorkflowClientBuilder();

        var usedSerializer = BuildAndGetSerializer(builder);

        Assert.IsType<JsonDaprSerializer>(usedSerializer);
        // Web defaults use camelCase
        var payload = usedSerializer.Serialize(new { MyValue = 1 });
        Assert.Contains("\"myValue\"", payload);
    }

    // -------------------------------------------------------------------------
    // gRPC client and logger resolution
    // -------------------------------------------------------------------------

    [Fact]
    public void Build_ShouldUseServiceProviderGrpcClientAndLogger()
    {
        var grpcClient = CreateGrpcClient();
        var logger = new TestLogger();

        var services = new ServiceCollection();
        services.AddSingleton(grpcClient);
        services.AddSingleton<ILoggerFactory>(new StubLoggerFactory(logger));

        var provider = services.BuildServiceProvider();

        var builder = new DaprWorkflowClientBuilder()
            .UseServiceProvider(provider)
            .UseSerializer(new StubDaprSerializer("x"));

        var client = builder.Build();
        var inner = GetInnerClient(client);

        var usedGrpcClient = GetPrivateField<TaskHubSidecarService.TaskHubSidecarServiceClient>(inner);
        var usedLogger = GetPrivateField<ILogger<WorkflowGrpcClient>>(inner);

        Assert.Same(grpcClient, usedGrpcClient);
        Assert.Same(logger, UnwrapLogger(usedLogger));
    }

    [Fact]
    public void Build_ShouldFallbackToNewGrpcClient_WhenServiceProviderHasNoGrpcClient()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

        var provider = services.BuildServiceProvider();

        var client = new DaprWorkflowClientBuilder().UseServiceProvider(provider).Build();
        var inner = GetInnerClient(client);
        var grpcClient = GetPrivateField<TaskHubSidecarService.TaskHubSidecarServiceClient>(inner);

        Assert.NotNull(grpcClient);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IDaprSerializer BuildAndGetSerializer(DaprWorkflowClientBuilder builder)
    {
        var client = builder.Build();
        var inner = GetInnerClient(client);
        return GetPrivateField<IDaprSerializer>(inner);
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

    // -------------------------------------------------------------------------
    // Stub types
    // -------------------------------------------------------------------------

    /// <summary>Custom IDaprSerializer stub — does not implement IWorkflowSerializer.</summary>
    private sealed class StubDaprSerializer(string name) : IDaprSerializer
    {
        public string Serialize(object? value, Type? inputType = null) => name;
        public T? Deserialize<T>(string? data) => default;
        public object? Deserialize(string? data, Type returnType) => null;
    }

    /// <summary>Custom IWorkflowSerializer stub — exercises the backward-compat interface.</summary>
    private sealed class StubWorkflowSerializer(string name) : IWorkflowSerializer
    {
        public string Serialize(object? value, Type? inputType = null) => name;
        public T? Deserialize<T>(string? data) => default;
        public object? Deserialize(string? data, Type returnType) => null;
    }

    private sealed record SerializerDependency(string Value);

    private sealed class TestLogger : ILogger<WorkflowGrpcClient>
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) { }
    }

    private sealed class StubLoggerFactory(ILogger logger) : ILoggerFactory
    {
        public void AddProvider(ILoggerProvider provider) { }
        public ILogger CreateLogger(string categoryName) => logger;
        public void Dispose() { }
    }

    private sealed class NoopScope : IDisposable
    {
        public static NoopScope Instance { get; } = new();
        public void Dispose() { }
    }
}
