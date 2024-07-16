using System;
using System.Linq;
using System.Net.Http;
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

        var clientBuilder = new Action<DaprJobsClientBuilder>(builder =>
            builder.UseDaprApiToken("abc"));
        
        services.AddDaprJobsClient(); //Doesn't set an API token value
        services.AddDaprJobsClient(clientBuilder); //Sets the API token value

        var serviceProvider = services.BuildServiceProvider();
        DaprJobsGrpcClient daprClient = serviceProvider.GetService<DaprJobsClient>() as DaprJobsGrpcClient;
        Assert.False(daprClient.httpClient.DefaultRequestHeaders.TryGetValues("dapr-api-token", out var _));
    }

    [Fact]
    public void AddDaprJobsClient_RegistersIHttpClientFactory()
    {
        var services = new ServiceCollection();

        services.AddDaprJobsClient();

        var serviceProvider = services.BuildServiceProvider();

        var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
        Assert.NotNull(httpClientFactory);

        var daprJobsClient = serviceProvider.GetService<DaprJobsClient>();
        Assert.NotNull(daprJobsClient);
    }

    [Fact]
    public void AddDaprJobsClient_RegistersUsingDependencyFromIServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TestSecretRetriever>();
        services.AddDaprJobsClient((provider, builder) =>
        {
            var configProvider = provider.GetRequiredService<TestSecretRetriever>();
            var daprApiToken = configProvider.GetApiTokenValue();

            builder.UseDaprApiToken(daprApiToken);
        });

        var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<DaprJobsClient>() as DaprJobsGrpcClient;
        var apiTokenValue = client.httpClient.DefaultRequestHeaders.GetValues("dapr-api-token").First();

        Assert.Equal("abcdef", apiTokenValue);
    }
    
    private class TestSecretRetriever
    {
        public string GetApiTokenValue() => "abcdef";
    }
}
