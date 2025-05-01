using Dapr.Cryptography.Encryption;
using Dapr.Cryptography.Encryption.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Crypto.Test.Encryption.Extensions;

public class DaprCryptographyServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDaprEncryptionClient_RegistersServicesWithSingletonLifetime()
    {
        var services = new ServiceCollection();
        var builder = services.AddDaprEncryptionClient();
        var servicesProvider = services.BuildServiceProvider();
        var daprClient = servicesProvider.GetService<DaprEncryptionClient>();

        Assert.NotNull(builder);
        Assert.NotNull(daprClient);
        Assert.Equal(ServiceLifetime.Singleton, services.First(sd => sd.ServiceType == typeof(DaprEncryptionClient)).Lifetime);
    }

    [Fact]
    public void AddDaprEncryptionClient_RegistersServicesWithScopedLifetime()
    {
        var services = new ServiceCollection();
        var builder = services.AddDaprEncryptionClient(lifetime: ServiceLifetime.Scoped);
        var serviceProvider = services.BuildServiceProvider();
        var daprClient = serviceProvider.GetService<DaprEncryptionClient>();

        Assert.NotNull(builder);
        Assert.NotNull(daprClient);
        Assert.Equal(ServiceLifetime.Scoped, services.First(sd => sd.ServiceType == typeof(DaprEncryptionClient)).Lifetime);
    }
    
    [Fact]
    public void AddDaprEncryptionClient_RegistersServicesWithTransientLifetime()
    {
        var services = new ServiceCollection();
        var builder = services.AddDaprEncryptionClient(lifetime: ServiceLifetime.Transient);
        var serviceProvider = services.BuildServiceProvider();
        var daprClient = serviceProvider.GetService<DaprEncryptionClient>();

        Assert.NotNull(builder);
        Assert.NotNull(daprClient);
        Assert.Equal(ServiceLifetime.Transient, services.First(sd => sd.ServiceType == typeof(DaprEncryptionClient)).Lifetime);
    }

    [Fact]
    public void AddDaprEncryptionClient_WithConfigureAction_ExecutesConfiguation()
    {
        var services = new ServiceCollection();
        var configureCalled = false;

        Action<IServiceProvider, DaprEncryptionClientBuilder> configure = (sp, builder) => configureCalled = true;

        var builder = services.AddDaprEncryptionClient(configure);
        var serviceProvider = services.BuildServiceProvider();
        var daprClient = serviceProvider.GetService<DaprEncryptionClient>();

        Assert.NotNull(builder);
        Assert.NotNull(daprClient);
        Assert.True(configureCalled);
    }
}
