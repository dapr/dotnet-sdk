using System;
using System.Text.Json;
using Dapr.Jobs.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dapr.Jobs.Test;

public class DaprJobsServiceCollectionExtensionsTest
{
    [Fact]
    public void AddDaprJobsClient_RegistersDaprClientOnlyOnce()
    {
        var services = new ServiceCollection();

        var clientBuilder = new Action<DaprJobClientBuilder>(builder =>
            builder.UseJsonSerializationOptions(new JsonSerializerOptions { PropertyNameCaseInsensitive = false }));

        //Registers with JsonSerializerOptions.PropertyNameCaseInsensitive = true (default)
        services.AddDaprJobsClient();
        //Register with PropertyNameCaseInsensitive = false
        services.AddDaprJobsClient(clientBuilder);

        var serviceProvider = services.BuildServiceProvider();
        DaprJobsGrpcClient daprClient = serviceProvider.GetService<DaprJobsClient>() as DaprJobsGrpcClient;
        Assert.True(daprClient.JsonSerializerOptions.PropertyNameCaseInsensitive);
    }

    [Fact]
    public void AddDaprJobsClient_RegistersUsingDependencyFromIServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TestConfigurationProvider>();
        services.AddDaprJobsClient((provider, builder) =>
        {
            var configProvider = provider.GetRequiredService<TestConfigurationProvider>();
            var caseSensitivity = configProvider.GetCaseSensitivity();

            builder.UseJsonSerializationOptions(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = caseSensitivity
            });
        });

        var serviceProvider = services.BuildServiceProvider();
        DaprJobsGrpcClient client = serviceProvider.GetRequiredService<DaprJobsClient>() as DaprJobsGrpcClient;

        //Registers with case-insensitive as true by default, but we set as false above
        Assert.False(client.JsonSerializerOptions.PropertyNameCaseInsensitive);
    }
    
    private class TestConfigurationProvider
    {
        public bool GetCaseSensitivity() => false;
    }
}
