using System.Text.Json;
using Dapr.Workflow.Serialization;
using Microsoft.Extensions.DependencyInjection;

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

    private sealed class MockSerializer : IWorkflowSerializer
    {
        public static MockSerializer Instance { get; } = new();

        public string Serialize(object? value, Type? inputType = null) => "mock";
        public T? Deserialize<T>(string? data) => default;
        public object? Deserialize(string? data, Type returnType) => null;
    }
}
