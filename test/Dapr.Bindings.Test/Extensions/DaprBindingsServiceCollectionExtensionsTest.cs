// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Dapr.Bindings.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Bindings.Test.Extensions;

public class DaprBindingsServiceCollectionExtensionsTest
{
    [Fact]

    public void AddDaprBindingsClient_FromIConfiguration()
    {
        const string apiToken = "abc123";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { { "DAPR_API_TOKEN", apiToken } })
            .Build();
        
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddDaprBindingsClient();

        var app = services.BuildServiceProvider();

        var jobsClient = app.GetRequiredService<DaprBindingsClient>() as DaprBindingsGrpcClient;
        
        Assert.NotNull(jobsClient!.DaprApiToken);
        Assert.Equal(apiToken, jobsClient.DaprApiToken);
    }
    
    [Fact]
    public void AddDaprBindingsClient_DaprClientRegistration_UseMostRecentVersion()
    {
        var services = new ServiceCollection();

        services.AddDaprBindingsClient((_, builder) =>
        {
            //Sets the API token value
            builder.UseDaprApiToken("abcd1234");
        }); 
        services.AddDaprBindingsClient(); //Sets a default API token value of an empty string
        
        var serviceProvider = services.BuildServiceProvider();
        var daprJobClient = serviceProvider.GetRequiredService<DaprBindingsClient>() as DaprBindingsGrpcClient;
        
        Assert.NotNull(daprJobClient!.HttpClient);
        Assert.False(daprJobClient.HttpClient.DefaultRequestHeaders.TryGetValues("dapr-api-token", out var _));
    }

    [Fact]
    public void AddDaprBindingsClient_RegistersIHttpClientFactory()
    {
        var services = new ServiceCollection();

        services.AddDaprBindingsClient();

        var serviceProvider = services.BuildServiceProvider();

        var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
        Assert.NotNull(httpClientFactory);

        var daprBindingsClient = serviceProvider.GetService<DaprBindingsClient>();
        Assert.NotNull(daprBindingsClient);
    }

    [Fact]
    public void AddDaprBindingsClient_RegistersUsingDependencyFromIServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TestSecretRetriever>();
        services.AddDaprBindingsClient((provider, builder) =>
        {
            var configProvider = provider.GetRequiredService<TestSecretRetriever>();
            var apiToken = configProvider.GetApiTokenValue();
            builder.UseDaprApiToken(apiToken);
        });

        var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<DaprBindingsClient>() as DaprBindingsGrpcClient;

        //Validate it's set on the GrpcClient - note that it doesn't get set on the HttpClient
        Assert.NotNull(client);
        Assert.NotNull(client.DaprApiToken);
        Assert.Equal("abcdef", client.DaprApiToken);
        Assert.NotNull(client.HttpClient);

        if (!client.HttpClient.DefaultRequestHeaders.TryGetValues("dapr-api-token", out var daprApiToken))
        {
            Assert.Fail();
        }
        Assert.Equal("abcdef", daprApiToken.FirstOrDefault());
    }
    
    [Fact]
    public void RegisterJobsClient_ShouldRegisterSingleton_WhenLifetimeIsSingleton()
    {
        var services = new ServiceCollection();

        services.AddDaprBindingsClient((_, _) => { }, ServiceLifetime.Singleton);
        var serviceProvider = services.BuildServiceProvider();

        var DaprBindingsClient1 = serviceProvider.GetService<DaprBindingsClient>();
        var DaprBindingsClient2 = serviceProvider.GetService<DaprBindingsClient>();

        Assert.NotNull(DaprBindingsClient1);
        Assert.NotNull(DaprBindingsClient2);
        
        Assert.Same(DaprBindingsClient1, DaprBindingsClient2);
    }

    [Fact]
    public async Task RegisterJobsClient_ShouldRegisterScoped_WhenLifetimeIsScoped()
    {
        var services = new ServiceCollection();

        services.AddDaprBindingsClient((_, _) => { }, ServiceLifetime.Scoped);
        var serviceProvider = services.BuildServiceProvider();

        await using var scope1 = serviceProvider.CreateAsyncScope();
        var DaprBindingsClient1 = scope1.ServiceProvider.GetService<DaprBindingsClient>();

        await using var scope2 = serviceProvider.CreateAsyncScope();
        var DaprBindingsClient2 = scope2.ServiceProvider.GetService<DaprBindingsClient>();
                
        Assert.NotNull(DaprBindingsClient1);
        Assert.NotNull(DaprBindingsClient2);
        Assert.NotSame(DaprBindingsClient1, DaprBindingsClient2);
    }

    [Fact]
    public void RegisterJobsClient_ShouldRegisterTransient_WhenLifetimeIsTransient()
    {
        var services = new ServiceCollection();

        services.AddDaprBindingsClient((_, _) => { }, ServiceLifetime.Transient);
        var serviceProvider = services.BuildServiceProvider();

        var DaprBindingsClient1 = serviceProvider.GetService<DaprBindingsClient>();
        var DaprBindingsClient2 = serviceProvider.GetService<DaprBindingsClient>();

        Assert.NotNull(DaprBindingsClient1);
        Assert.NotNull(DaprBindingsClient2);
        Assert.NotSame(DaprBindingsClient1, DaprBindingsClient2);
    }

    private class TestSecretRetriever
    {
        public string GetApiTokenValue() => "abcdef";
    }
}
