using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dapr.Actors.AspNetCore.Test;

public sealed class DaprActorServiceCollectionExtensionsTest
{
    [Fact]
    public void RegisterActorsClient_ShouldRegisterSingleton_WhenLifetimeIsSingleton()
    {
        var services = new ServiceCollection();

        services.AddActors(options => { }, ServiceLifetime.Singleton);
        var serviceProvider = services.BuildServiceProvider();

        var daprClient1 = serviceProvider.GetService<Runtime.ActorRuntime>();
        var daprClient2 = serviceProvider.GetService<Runtime.ActorRuntime>();

        Assert.NotNull(daprClient1);
        Assert.NotNull(daprClient2);
        
        Assert.Same(daprClient1, daprClient2);
    }

    [Fact]
    public async Task RegisterActorsClient_ShouldRegisterScoped_WhenLifetimeIsScoped()
    {
        var services = new ServiceCollection();

        services.AddActors(options => { }, ServiceLifetime.Scoped);
        var serviceProvider = services.BuildServiceProvider();

        await using var scope1 = serviceProvider.CreateAsyncScope();
        var daprClient1 = scope1.ServiceProvider.GetService<Runtime.ActorRuntime>();

        await using var scope2 = serviceProvider.CreateAsyncScope();
        var daprClient2 = scope2.ServiceProvider.GetService<Runtime.ActorRuntime>();
                
        Assert.NotNull(daprClient1);
        Assert.NotNull(daprClient2);
        Assert.NotSame(daprClient1, daprClient2);
    }

    [Fact]
    public void RegisterActorsClient_ShouldRegisterTransient_WhenLifetimeIsTransient()
    {
        var services = new ServiceCollection();

        services.AddActors(options => { }, ServiceLifetime.Transient);
        var serviceProvider = services.BuildServiceProvider();

        var daprClient1 = serviceProvider.GetService<Runtime.ActorRuntime>();
        var daprClient2 = serviceProvider.GetService<Runtime.ActorRuntime>();

        Assert.NotNull(daprClient1);
        Assert.NotNull(daprClient2);
        Assert.NotSame(daprClient1, daprClient2);
    }
}
