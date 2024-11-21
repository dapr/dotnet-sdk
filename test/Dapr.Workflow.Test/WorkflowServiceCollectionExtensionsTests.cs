using System.Text.Json;
using Dapr.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Workflow.Test;

public class WorkflowServiceCollectionExtensionsTests
{
    [Fact]
    public void ConfigureDaprClient_FromWorkflowClientRegistration()
    {
        const int maxDepth = 4;
        const bool writeIndented = true;
        
        var services = new ServiceCollection();

        services.AddDaprWorkflow(options => { }, builder =>
        {
            builder.UseJsonSerializationOptions(new JsonSerializerOptions { MaxDepth = maxDepth, WriteIndented = writeIndented });
        });

        var serviceProvider = services.BuildServiceProvider();

        var daprClient = serviceProvider.GetRequiredService<DaprClient>();
        Assert.Equal(maxDepth, daprClient.JsonSerializerOptions.MaxDepth);
        Assert.Equal(writeIndented, daprClient.JsonSerializerOptions.WriteIndented);
    }
    
    [Fact]
    public void RegisterWorkflowClient_ShouldRegisterSingleton_WhenLifetimeIsSingleton()
    {
        var services = new ServiceCollection();

        services.AddDaprWorkflow(options => { }, lifetime: ServiceLifetime.Singleton);
        var serviceProvider = services.BuildServiceProvider();

        var daprWorkflowClient1 = serviceProvider.GetService<DaprWorkflowClient>();
        var daprWorkflowClient2 = serviceProvider.GetService<DaprWorkflowClient>();

        Assert.NotNull(daprWorkflowClient1);
        Assert.NotNull(daprWorkflowClient2);
        
        Assert.Same(daprWorkflowClient1, daprWorkflowClient2);
    }

    [Fact]
    public async Task RegisterWorkflowClient_ShouldRegisterScoped_WhenLifetimeIsScoped()
    {
        var services = new ServiceCollection();

        services.AddDaprWorkflow(options => { }, lifetime: ServiceLifetime.Scoped);
        var serviceProvider = services.BuildServiceProvider();

        await using var scope1 = serviceProvider.CreateAsyncScope();
        var daprWorkflowClient1 = scope1.ServiceProvider.GetService<DaprWorkflowClient>();

        await using var scope2 = serviceProvider.CreateAsyncScope();
        var daprWorkflowClient2 = scope2.ServiceProvider.GetService<DaprWorkflowClient>();
                
        Assert.NotNull(daprWorkflowClient1);
        Assert.NotNull(daprWorkflowClient2);
        Assert.NotSame(daprWorkflowClient1, daprWorkflowClient2);
    }

    [Fact]
    public void RegisterWorkflowClient_ShouldRegisterTransient_WhenLifetimeIsTransient()
    {
        var services = new ServiceCollection();

        services.AddDaprWorkflow(options => { }, lifetime: ServiceLifetime.Transient);
        var serviceProvider = services.BuildServiceProvider();

        var daprWorkflowClient1 = serviceProvider.GetService<DaprWorkflowClient>();
        var daprWorkflowClient2 = serviceProvider.GetService<DaprWorkflowClient>();

        Assert.NotNull(daprWorkflowClient1);
        Assert.NotNull(daprWorkflowClient2);
        Assert.NotSame(daprWorkflowClient1, daprWorkflowClient2);
    }
}
