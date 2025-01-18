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

using Dapr.Messaging.PublishSubscribe;
using Dapr.Messaging.PublishSubscribe.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Messaging.Test.Extensions;

public sealed class PublishSubscribeServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDaprMessagingClient_FromIConfiguration()
    {
        const string apiToken = "abc123";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"DAPR_API_TOKEN", apiToken }
            })
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);

        services.AddDaprPubSubClient();

        var app = services.BuildServiceProvider();

        var pubSubClient = app.GetRequiredService<DaprPublishSubscribeClient>() as DaprPublishSubscribeGrpcClient;

        Assert.NotNull(pubSubClient!);
        Assert.Equal(apiToken, pubSubClient.DaprApiToken);
    }
    
    [Fact]
    public void AddDaprPubSubClient_RegistersIHttpClientFactory()
    {
        var services = new ServiceCollection();
        services.AddDaprPubSubClient();

        var serviceProvider = services.BuildServiceProvider();
        var daprClient = serviceProvider.GetService<DaprPublishSubscribeClient>();
        Assert.NotNull(daprClient);
    }

    [Fact]
    public void AddDaprPubSubClient_CallsConfigureAction()
    {
        var services = new ServiceCollection();

        var configureCalled = false;

        services.AddDaprPubSubClient(Configure);

        var serviceProvider = services.BuildServiceProvider();
        var daprClient = serviceProvider.GetService<DaprPublishSubscribeClient>();
        Assert.NotNull(daprClient);
        Assert.True(configureCalled);
        return;

        void Configure(IServiceProvider sp, DaprPublishSubscribeClientBuilder builder)
        {
            configureCalled = true;
        }
    }   

    [Fact]
    public void AddDaprPubSubClient_RegistersServicesCorrectly()
    {
        var services = new ServiceCollection();
        services.AddDaprPubSubClient();
        var serviceProvider = services.BuildServiceProvider();
      
        var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
        Assert.NotNull(httpClientFactory);

        var daprPubSubClient = serviceProvider.GetService<DaprPublishSubscribeClient>();
        Assert.NotNull(daprPubSubClient);
    }
    
    [Fact]
    public void RegisterPubsubClient_ShouldRegisterSingleton_WhenLifetimeIsSingleton()
    {
        var services = new ServiceCollection();

        services.AddDaprPubSubClient(lifetime: ServiceLifetime.Singleton);
        var serviceProvider = services.BuildServiceProvider();

        var daprPubSubClient1 = serviceProvider.GetService<DaprPublishSubscribeClient>();
        var daprPubSubClient2 = serviceProvider.GetService<DaprPublishSubscribeClient>();

        Assert.NotNull(daprPubSubClient1);
        Assert.NotNull(daprPubSubClient2);
        
        Assert.Same(daprPubSubClient1, daprPubSubClient2);
    }

    [Fact]
    public async Task RegisterPubsubClient_ShouldRegisterScoped_WhenLifetimeIsScoped()
    {
        var services = new ServiceCollection();

        services.AddDaprPubSubClient(lifetime: ServiceLifetime.Scoped);
        var serviceProvider = services.BuildServiceProvider();

        await using var scope1 = serviceProvider.CreateAsyncScope();
        var daprPubSubClient1 = scope1.ServiceProvider.GetService<DaprPublishSubscribeClient>();

        await using var scope2 = serviceProvider.CreateAsyncScope();
        var daprPubSubClient2 = scope2.ServiceProvider.GetService<DaprPublishSubscribeClient>();
                
        Assert.NotNull(daprPubSubClient1);
        Assert.NotNull(daprPubSubClient2);
        Assert.NotSame(daprPubSubClient1, daprPubSubClient2);
    }

    [Fact]
    public void RegisterPubsubClient_ShouldRegisterTransient_WhenLifetimeIsTransient()
    {
        var services = new ServiceCollection();

        services.AddDaprPubSubClient(lifetime: ServiceLifetime.Transient);
        var serviceProvider = services.BuildServiceProvider();

        var daprPubSubClient1 = serviceProvider.GetService<DaprPublishSubscribeClient>();
        var daprPubSubClient2 = serviceProvider.GetService<DaprPublishSubscribeClient>();

        Assert.NotNull(daprPubSubClient1);
        Assert.NotNull(daprPubSubClient2);
        Assert.NotSame(daprPubSubClient1, daprPubSubClient2);
    }
}
