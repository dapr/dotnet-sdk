using System;
using System.Linq;
using System.Net.Http;
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
        
        services.AddDaprJobsClient(); //Sets a default API token value of an empty string
        services.AddDaprJobsClient(clientBuilder); //Sets the API token value

        var serviceProvider = services.BuildServiceProvider();
        DaprJobsGrpcClient daprJobClient = serviceProvider.GetService<DaprJobsClient>() as DaprJobsGrpcClient;

        Assert.Null(daprJobClient!.apiTokenHeader);
        Assert.False(daprJobClient.httpClient.DefaultRequestHeaders.TryGetValues("dapr-api-token", out var _));
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

        //Validate it's set on the HttpClient
        var apiTokenValue = client.httpClient.DefaultRequestHeaders.GetValues("dapr-api-token").First();
        Assert.Equal("abcdef", apiTokenValue);

        //Validate it's set in the apiTokenHeader property
        Assert.NotNull(client.apiTokenHeader);
        Assert.Equal("dapr-api-token", client.apiTokenHeader.Value.Key);
        Assert.Equal("abcdef", client.apiTokenHeader.Value.Value);
    }
    
    private class TestSecretRetriever
    {
        public string GetApiTokenValue() => "abcdef";
    }
}
