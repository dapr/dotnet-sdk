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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Dapr.Cryptography.Encryption;
using Dapr.Cryptography.Encryption.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Cryptography.Test.Encryption.Extensions;

public class EncryptionServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDaprEncryptionClient_FromIConfiguration()
    {
        const string apiToken = "acb123";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { { "DAPR_API_TOKEN", apiToken } })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddDaprEncryptionClient();

        var app = services.BuildServiceProvider();

        var encryptionClient = app.GetRequiredService<DaprEncryptionClient>() as DaprEncryptionGrpcClient;

        Assert.NotNull(encryptionClient!.DaprApiToken);
        Assert.Equal(apiToken, encryptionClient.DaprApiToken);
    }

    [Fact]
    public void AddDaprEncryptionClient_RegistersClientOnlyOnce()
    {
        var services = new ServiceCollection();

        var clientBuilder = new Action<IServiceProvider, DaprEncryptionClientBuilder>((sp, builder) =>
        {
            builder.UseDaprApiToken("abc");
        });

        services.AddDaprEncryptionClient(); //Sets a default API token value of an empty string
        services.AddDaprEncryptionClient(clientBuilder); //Sets the API token value

        var serviceProvider = services.BuildServiceProvider();
        var daprEncryptionClient = serviceProvider.GetService<DaprEncryptionClient>() as DaprEncryptionGrpcClient;

        Assert.NotNull(daprEncryptionClient!.HttpClient);
        Assert.False(daprEncryptionClient.HttpClient.DefaultRequestHeaders.TryGetValues("dapr-api-token", out _));
    }

    [Fact]
    public void AddDaprEncryptionClient_RegistersIHttpClientFactory()
    {
        var services = new ServiceCollection();

        services.AddDaprEncryptionClient();

        var serviceProvider = services.BuildServiceProvider();

        var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
        Assert.NotNull(httpClientFactory);

        var daprEncryptionClient = serviceProvider.GetService<DaprEncryptionClient>();
        Assert.NotNull(daprEncryptionClient);
    }

    [Fact]
    public void AddDaprEncryptionClient_RegistersUsingDependencyFromIServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TestSecretRetriever>();
        services.AddDaprEncryptionClient((provider, builder) =>
        {
            var configProvider = provider.GetRequiredService<TestSecretRetriever>();
            var apiToken = configProvider.GetApiTokenValue();
            builder.UseDaprApiToken(apiToken);
        });

        var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<DaprEncryptionClient>() as DaprEncryptionGrpcClient;
        
        //Validate it's set on the GrpcClient - not that it doesn't get set on the HttpClient
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
    public void AddDaprEncryptionClient_ShouldRegisterSingleton_WhenLifetimeIsSingleton()
    {
        var services = new ServiceCollection();

        services.AddDaprEncryptionClient((_, _) => { }, ServiceLifetime.Singleton);
        var serviceProvider = services.BuildServiceProvider();

        var encryptionClient1 = serviceProvider.GetService<DaprEncryptionClient>();
        var encryptionClient2 = serviceProvider.GetService<DaprEncryptionClient>();

        Assert.NotNull(encryptionClient1);
        Assert.NotNull(encryptionClient2);
        
        Assert.Same(encryptionClient1, encryptionClient2);
    }
    
    [Fact]
    public void AddDaprEncryptionClient_ShouldRegisterTransient_WhenLifetimeIsTransient()
    {
        var services = new ServiceCollection();

        services.AddDaprEncryptionClient((_, _) => { }, ServiceLifetime.Transient);
        var serviceProvider = services.BuildServiceProvider();

        var encryptionClient1 = serviceProvider.GetService<DaprEncryptionClient>();
        var encryptionClient2 = serviceProvider.GetService<DaprEncryptionClient>();

        Assert.NotNull(encryptionClient1);
        Assert.NotNull(encryptionClient2);
    }
    
    [Fact]
    public async Task AddDaprEncryptionClient_ShouldRegisterScoped_WhenLifetimeIsScoped()
    {
        var services = new ServiceCollection();

        services.AddDaprEncryptionClient((_, _) => { }, ServiceLifetime.Scoped);
        var serviceProvider = services.BuildServiceProvider();

        await using var scope1 = serviceProvider.CreateAsyncScope();
        var encryptionClient1 = scope1.ServiceProvider.GetService<DaprEncryptionClient>();
        Assert.NotNull(encryptionClient1);
        
        await using var scope2 = serviceProvider.CreateAsyncScope();
        var encryptionClient2 = scope2.ServiceProvider.GetService<DaprEncryptionClient>();
        Assert.NotNull(encryptionClient2);

        Assert.NotSame(encryptionClient1, encryptionClient2);
    }
    
    private sealed class TestSecretRetriever
    {
        public string GetApiTokenValue() => "abcdef";
    }
}
