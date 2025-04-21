using Dapr.Crypto.Encryption.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Crypto.Test.Encryption.Extensions;

public class DaprCryptoBuilderTests
{
    [Fact]
    public void Constructor_InitializesServiceProperty()
    {
        var services = new ServiceCollection();
        var builder = new DaprCryptoBuilder(services);

        Assert.NotNull(builder.Services);
        Assert.Equal(services, builder.Services);
    }

    [Fact]
    public void ServicesProperty_ReturnsRegisteredServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<object>();

        var builder = new DaprCryptoBuilder(services);

        Assert.NotNull(builder.Services);
        Assert.Single(builder.Services);
    }
}
