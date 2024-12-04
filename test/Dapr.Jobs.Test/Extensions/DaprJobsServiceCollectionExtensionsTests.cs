﻿// ------------------------------------------------------------------------
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
using System.Net.Http;
using System.Threading.Tasks;
using Dapr.Jobs.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Jobs.Test.Extensions;

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
        var daprJobClient = serviceProvider.GetService<DaprJobsClient>() as DaprJobsGrpcClient;

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

        //Validate it's set on the GrpcClient - note that it doesn't get set on the HttpClient
        Assert.NotNull(client);
        Assert.NotNull(client.apiTokenHeader);
        Assert.True(client.apiTokenHeader.HasValue);
        Assert.Equal("dapr-api-token", client.apiTokenHeader.Value.Key);
        Assert.Equal("abcdef", client.apiTokenHeader.Value.Value);
    }
    
    [Fact]
    public void RegisterJobsClient_ShouldRegisterSingleton_WhenLifetimeIsSingleton()
    {
        var services = new ServiceCollection();

        services.AddDaprJobsClient(options => { }, ServiceLifetime.Singleton);
        var serviceProvider = services.BuildServiceProvider();

        var daprJobsClient1 = serviceProvider.GetService<DaprJobsClient>();
        var daprJobsClient2 = serviceProvider.GetService<DaprJobsClient>();

        Assert.NotNull(daprJobsClient1);
        Assert.NotNull(daprJobsClient2);
        
        Assert.Same(daprJobsClient1, daprJobsClient2);
    }

    [Fact]
    public async Task RegisterJobsClient_ShouldRegisterScoped_WhenLifetimeIsScoped()
    {
        var services = new ServiceCollection();

        services.AddDaprJobsClient(options => { }, ServiceLifetime.Scoped);
        var serviceProvider = services.BuildServiceProvider();

        await using var scope1 = serviceProvider.CreateAsyncScope();
        var daprJobsClient1 = scope1.ServiceProvider.GetService<DaprJobsClient>();

        await using var scope2 = serviceProvider.CreateAsyncScope();
        var daprJobsClient2 = scope2.ServiceProvider.GetService<DaprJobsClient>();
                
        Assert.NotNull(daprJobsClient1);
        Assert.NotNull(daprJobsClient2);
        Assert.NotSame(daprJobsClient1, daprJobsClient2);
    }

    [Fact]
    public void RegisterJobsClient_ShouldRegisterTransient_WhenLifetimeIsTransient()
    {
        var services = new ServiceCollection();

        services.AddDaprJobsClient(options => { }, ServiceLifetime.Transient);
        var serviceProvider = services.BuildServiceProvider();

        var daprJobsClient1 = serviceProvider.GetService<DaprJobsClient>();
        var daprJobsClient2 = serviceProvider.GetService<DaprJobsClient>();

        Assert.NotNull(daprJobsClient1);
        Assert.NotNull(daprJobsClient2);
        Assert.NotSame(daprJobsClient1, daprJobsClient2);
    }

    private class TestSecretRetriever
    {
        public string GetApiTokenValue() => "abcdef";
    }
}