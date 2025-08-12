// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.AI.Test.Conversation.Extensions;

public class DaprAiConversationBuilderExtensionsTest
{
    [Fact]
    public void AddDaprConversationClient_FromIConfiguration()
    {
        const string apiToken = "abc123";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { { "DAPR_API_TOKEN", apiToken } })
            .Build();
        
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddDaprConversationClient();

        var app = services.BuildServiceProvider();

        var conversationClient = app.GetRequiredService<DaprConversationClient>() as DaprConversationClient;
        
        Assert.NotNull(conversationClient!.DaprApiToken);
        Assert.Equal(apiToken, conversationClient.DaprApiToken);
    }
    
    [Fact]
    public void AddDaprConversationClient_RegistersDaprClient_UsesMostRecentRegistration()
    {
        var services = new ServiceCollection();

        services.AddDaprConversationClient((_, builder) =>
        {
            builder.UseDaprApiToken("abc123");
        }); //Sets the API token value
        services.AddDaprConversationClient(); //Sets a default API token value of an empty string
        
        var serviceProvider = services.BuildServiceProvider();
        var daprConversationClient = serviceProvider.GetService<DaprConversationClient>();
        
        Assert.NotNull(daprConversationClient!.HttpClient);
        Assert.False(daprConversationClient.HttpClient.DefaultRequestHeaders.TryGetValues("dapr-api-token", out var _));
    }
    
    [Fact]
    public void AddDaprConversationClient_RegistersUsingDependencyFromIServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TestSecretRetriever>();
        services.AddDaprConversationClient((provider, builder) =>
        {
            var configProvider = provider.GetRequiredService<TestSecretRetriever>();
            var apiToken = configProvider.GetApiTokenValue();
            builder.UseDaprApiToken(apiToken);
        });

        var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<DaprConversationClient>();

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
    public void AddDaprConversationClient_WithoutConfigure_ShouldAddServices()
    {
        var services = new ServiceCollection();
        var builder = services.AddDaprConversationClient();
        Assert.NotNull(builder);
    }

    [Fact]
    public void AddDaprConversationClient_RegistersIHttpClientFactory()
    {
        var services = new ServiceCollection();
        services.AddDaprConversationClient();
        var serviceProvider = services.BuildServiceProvider();

        var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
        Assert.NotNull(httpClientFactory);

        var daprConversationClient = serviceProvider.GetService<DaprConversationClient>();
        Assert.NotNull(daprConversationClient);
    }

    [Fact]
    public void AddDaprConversationClient_NullServices_ShouldThrowException()
    {
        IServiceCollection services = null;
        Assert.Throws<ArgumentNullException>(() => services.AddDaprConversationClient());
    }
    
    [Fact]
    public void AddDaprConversationClient_ShouldRegisterSingleton_WhenLifetimeIsSingleton()
    {
        var services = new ServiceCollection();

        services.AddDaprConversationClient((_, _) => { }, ServiceLifetime.Singleton);
        var serviceProvider = services.BuildServiceProvider();

        var daprConversationClient1 = serviceProvider.GetService<DaprConversationClient>();
        var daprConversationClient2 = serviceProvider.GetService<DaprConversationClient>();

        Assert.NotNull(daprConversationClient1);
        Assert.NotNull(daprConversationClient2);
        
        Assert.Same(daprConversationClient1, daprConversationClient2);
    }

    [Fact]
    public async Task AddDaprConversationClient_ShouldRegisterScoped_WhenLifetimeIsScoped()
    {
        var services = new ServiceCollection();

        services.AddDaprConversationClient((_, _) => { }, ServiceLifetime.Scoped);
        var serviceProvider = services.BuildServiceProvider();

        await using var scope1 = serviceProvider.CreateAsyncScope();
        var daprConversationClient1 = scope1.ServiceProvider.GetService<DaprConversationClient>();

        await using var scope2 = serviceProvider.CreateAsyncScope();
        var daprConversationClient2 = scope2.ServiceProvider.GetService<DaprConversationClient>();
                
        Assert.NotNull(daprConversationClient1);
        Assert.NotNull(daprConversationClient2);
        Assert.NotSame(daprConversationClient1, daprConversationClient2);
    }

    [Fact]
    public void AddDaprConversationClient_ShouldRegisterTransient_WhenLifetimeIsTransient()
    {
        var services = new ServiceCollection();

        services.AddDaprConversationClient((_, _) => { }, ServiceLifetime.Transient);
        var serviceProvider = services.BuildServiceProvider();

        var daprConversationClient1 = serviceProvider.GetService<DaprConversationClient>();
        var daprConversationClient2 = serviceProvider.GetService<DaprConversationClient>();

        Assert.NotNull(daprConversationClient1);
        Assert.NotNull(daprConversationClient2);
        Assert.NotSame(daprConversationClient1, daprConversationClient2);
    }
    
    private class TestSecretRetriever
    {
        public string GetApiTokenValue() => "abcdef";
    }
}
