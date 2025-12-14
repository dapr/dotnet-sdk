using System.Text.Json;
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Abstractions;
using Dapr.Workflow.Client;
using Dapr.Workflow.Serialization;
using Dapr.Workflow.Worker;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Dapr.Workflow.Test;

public class WorkflowServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDaprWorkflow_ShouldThrowArgumentNullException_WhenServiceCollectionIsNull()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() => services.AddDaprWorkflow(_ => { }));
    }

    [Fact]
    public void AddDaprWorkflow_ShouldThrowArgumentOutOfRangeException_WhenLifetimeIsInvalid()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentOutOfRangeException>(() => services.AddDaprWorkflow(_ => { }, (ServiceLifetime)999));
    }

    [Fact]
    public void AddDaprWorkflowSerializer_InstanceOverload_ShouldThrowArgumentNullException_WhenArgsAreNull()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddDaprWorkflowSerializer(MockSerializer.Instance));
        Assert.Throws<ArgumentNullException>(() => services.AddDaprWorkflowSerializer((IWorkflowSerializer)null!));
    }

    [Fact]
    public void AddDaprWorkflowSerializer_FactoryOverload_ShouldThrowArgumentNullException_WhenArgsAreNull()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddDaprWorkflowSerializer(_ => MockSerializer.Instance));
        Assert.Throws<ArgumentNullException>(() => services.AddDaprWorkflowSerializer((Func<IServiceProvider, IWorkflowSerializer>)null!));
    }

    [Fact]
    public void AddDaprWorkflowJsonSerializer_ShouldThrowArgumentNullException_WhenArgsAreNull()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddDaprWorkflowJsonSerializer(new JsonSerializerOptions()));
        Assert.Throws<ArgumentNullException>(() => services.AddDaprWorkflowJsonSerializer(null!));
    }

    [Fact]
    public void AddDaprWorkflow_ShouldUseCustomSerializer_WhenRegisteredBeforeCallingAddDaprWorkflow()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDaprWorkflowSerializer(MockSerializer.Instance);

        services.AddDaprWorkflow(_ => { });

        var sp = services.BuildServiceProvider();

        var serializer = sp.GetRequiredService<IWorkflowSerializer>();

        Assert.Same(MockSerializer.Instance, serializer);
    }

    [Fact]
    public void AddDaprWorkflowJsonSerializer_ShouldReplaceDefaultSerializer()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDaprWorkflowJsonSerializer(new JsonSerializerOptions { PropertyNamingPolicy = null });

        services.AddDaprWorkflow(_ => { });

        var sp = services.BuildServiceProvider();

        var serializer = sp.GetRequiredService<IWorkflowSerializer>();

        Assert.IsType<JsonWorkflowSerializer>(serializer);
    }
    
    [Fact]
    public void AddDaprWorkflow_ShouldApplyGrpcChannelOptionsIntoGrpcClientFactoryOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDaprWorkflow(options =>
        {
            options.UseGrpcChannelOptions(new GrpcChannelOptions
            {
                MaxReceiveMessageSize = 1234,
                MaxSendMessageSize = 5678
            });
        });

        var sp = services.BuildServiceProvider();

        var monitor = sp.GetRequiredService<IOptionsMonitor<GrpcClientFactoryOptions>>();

        var clientType = typeof(TaskHubSidecarService.TaskHubSidecarServiceClient);

        var grpcOptions =
            monitor.Get(clientType.FullName!)
            ?? monitor.Get(clientType.Name);

        if (grpcOptions.ChannelOptionsActions.Count == 0)
        {
            grpcOptions = monitor.Get(clientType.Name);
        }

        Assert.NotNull(grpcOptions);
        Assert.NotEmpty(grpcOptions.ChannelOptionsActions);

        var channelOptions = new GrpcChannelOptions();
        foreach (var action in grpcOptions.ChannelOptionsActions)
        {
            action(channelOptions);
        }

        Assert.Equal(1234, channelOptions.MaxReceiveMessageSize);
        Assert.Equal(5678, channelOptions.MaxSendMessageSize);
    }

    [Fact]
    public async Task AddDaprWorkflow_ShouldCreateWorkflowsFactory_AndApplyRegistrationsFromOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

        services.AddDaprWorkflow(options =>
        {
            options.RegisterWorkflow<int, int>("wf", (_, x) => Task.FromResult(x + 1));
            options.RegisterActivity<int, int>("act", (_, x) => Task.FromResult(x + 2));
        });

        var sp = services.BuildServiceProvider();

        var factory = sp.GetRequiredService<IWorkflowsFactory>();

        Assert.True(factory.TryCreateWorkflow(new TaskIdentifier("wf"), sp, out var wf));
        Assert.NotNull(wf);

        var wfResult = await wf!.RunAsync(new FakeWorkflowContext(), 10);
        Assert.Equal(11, wfResult);

        Assert.True(factory.TryCreateActivity(new TaskIdentifier("act"), sp, out var act));
        Assert.NotNull(act);

        var actResult = await act!.RunAsync(new FakeActivityContext(), 10);
        Assert.Equal(12, actResult);
    }

    [Fact]
    public void AddDaprWorkflow_ShouldRegisterWorkflowClientImplementation_AsWorkflowGrpcClient()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

        // Provide a concrete proto client so the WorkflowClient factory can be executed.
        var callInvoker = new Mock<CallInvoker>(MockBehavior.Loose);
        services.AddSingleton(new TaskHubSidecarService.TaskHubSidecarServiceClient(callInvoker.Object));

        services.AddDaprWorkflow(_ => { });

        var sp = services.BuildServiceProvider();

        var workflowClient = sp.GetRequiredService<WorkflowClient>();

        Assert.NotNull(workflowClient);
        Assert.IsType<WorkflowGrpcClient>(workflowClient);
    }

    [Fact]
    public void AddDaprWorkflow_ShouldRegisterWorkflowWorker_AsHostedService()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

        // These are required for the hosted service construction to be possible.
        var callInvoker = new Mock<CallInvoker>(MockBehavior.Loose);
        services.AddSingleton(new TaskHubSidecarService.TaskHubSidecarServiceClient(callInvoker.Object));

        services.AddDaprWorkflow(_ => { });

        var hostedDescriptors = services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .ToList();

        Assert.Contains(hostedDescriptors, d => d.ImplementationType == typeof(WorkflowWorker));
    }
    
    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void AddDaprWorkflow_ShouldRegisterDaprWorkflowClient_WithConfiguredLifetime(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

        services.AddDaprWorkflow(_ => { }, lifetime);

        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DaprWorkflowClient));
        Assert.NotNull(descriptor);
        Assert.Equal(lifetime, descriptor!.Lifetime);
    }

    [Fact]
    public void AddDaprWorkflowSerializer_FactoryOverload_ShouldResolveDependenciesFromServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

        services.AddSingleton<SerializerDependency>(new SerializerDependency("dep-1"));

        services.AddDaprWorkflowSerializer(sp =>
        {
            var dep = sp.GetRequiredService<SerializerDependency>();
            return new DependencyBasedSerializer(dep);
        });

        services.AddSingleton<WorkflowClient>(new FakeWorkflowClient());

        services.AddDaprWorkflow(_ => { });

        var sp = services.BuildServiceProvider();

        var serializer = sp.GetRequiredService<IWorkflowSerializer>();

        var typed = Assert.IsType<DependencyBasedSerializer>(serializer);
        Assert.Equal("dep-1", typed.Dep.Value);
    }
    
    private sealed record SerializerDependency(string Value);

    private sealed class DependencyBasedSerializer(SerializerDependency dep) : IWorkflowSerializer
    {
        public SerializerDependency Dep { get; } = dep;

        public string Serialize(object? value, Type? inputType = null) => "x";
        public T? Deserialize<T>(string? data) => default;
        public object? Deserialize(string? data, Type returnType) => null;
    }
    
    private sealed class FakeWorkflowClient : WorkflowClient
    {
        public override Task<string> ScheduleNewWorkflowAsync(string workflowName, object? input = null, StartWorkflowOptions? options = null, CancellationToken cancellation = default)
            => throw new NotSupportedException();

        public override Task<WorkflowMetadata?> GetWorkflowMetadataAsync(string instanceId, bool getInputsAndOutputs = true, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public override Task<WorkflowMetadata> WaitForWorkflowStartAsync(string instanceId, bool getInputsAndOutputs = true, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public override Task<WorkflowMetadata> WaitForWorkflowCompletionAsync(string instanceId, bool getInputsAndOutputs = true, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public override Task RaiseEventAsync(string instanceId, string eventName, object? eventPayload = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public override Task TerminateWorkflowAsync(string instanceId, object? output = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public override Task SuspendWorkflowAsync(string instanceId, string? reason = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public override Task ResumeWorkflowAsync(string instanceId, string? reason = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public override Task<bool> PurgeInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    
    private sealed class FakeWorkflowContext : WorkflowContext
    {
        public override string Name => "wf";
        public override string InstanceId => "i";
        public override DateTime CurrentUtcDateTime => DateTime.UtcNow;
        public override bool IsReplaying => false;

        public override Task<T> CallActivityAsync<T>(string name, object? input = null, WorkflowTaskOptions? options = null) => throw new NotSupportedException();
        public override Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<T> WaitForExternalEventAsync<T>(string eventName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public override Task<T> WaitForExternalEventAsync<T>(string eventName, TimeSpan timeout) => throw new NotSupportedException();
        public override void SendEvent(string instanceId, string eventName, object payload) => throw new NotSupportedException();
        public override void SetCustomStatus(object? customStatus) => throw new NotSupportedException();
        public override Task<TResult> CallChildWorkflowAsync<TResult>(string workflowName, object? input = null, ChildWorkflowTaskOptions? options = null) => throw new NotSupportedException();
        public override void ContinueAsNew(object? newInput = null, bool preserveUnprocessedEvents = true) => throw new NotSupportedException();
        public override Guid NewGuid() => Guid.NewGuid();
        public override ILogger CreateReplaySafeLogger(string categoryName) => throw new NotSupportedException();
        public override ILogger CreateReplaySafeLogger(Type type) => throw new NotSupportedException();
        public override ILogger CreateReplaySafeLogger<T>() => throw new NotSupportedException();
    }

    private sealed class FakeActivityContext : WorkflowActivityContext
    {
        public override TaskIdentifier Identifier => new("act");
        public override string InstanceId => "i";
    }

    private sealed class MockSerializer : IWorkflowSerializer
    {
        public static MockSerializer Instance { get; } = new();

        public string Serialize(object? value, Type? inputType = null) => "mock";
        public T? Deserialize<T>(string? data) => default;
        public object? Deserialize(string? data, Type returnType) => null;
    }
}
