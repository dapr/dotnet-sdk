// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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

using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Dapr.DistributedLock.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.DistributedLock.Test.Extensions;

public class DaprDistributedLockServiceCollectionExtensionsTest
{
    [Fact]
    public void AddDaprDistributedLock_RegistersDaprDistributedLockClient()
    {
        var services = new ServiceCollection();
        services.AddDaprDistributedLock();

        var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetService<DaprDistributedLockClient>();

        Assert.NotNull(client);
    }

    [Fact]
    public void AddDaprDistributedLock_RegistersIHttpClientFactory()
    {
        var services = new ServiceCollection();
        services.AddDaprDistributedLock();

        var serviceProvider = services.BuildServiceProvider();
        var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();

        Assert.NotNull(httpClientFactory);
    }

    [Fact]
    public void AddDaprDistributedLock_DefaultLifetimeIsScoped()
    {
        var services = new ServiceCollection();
        services.AddDaprDistributedLock();

        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(DaprDistributedLockClient));

        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor!.Lifetime);
    }

    [Fact]
    public void AddDaprDistributedLock_SingletonLifetime_RegistersAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddDaprDistributedLock(lifetime: ServiceLifetime.Singleton);

        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(DaprDistributedLockClient));

        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor!.Lifetime);
    }

    [Fact]
    public void AddDaprDistributedLock_TransientLifetime_RegistersAsTransient()
    {
        var services = new ServiceCollection();
        services.AddDaprDistributedLock(lifetime: ServiceLifetime.Transient);

        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(DaprDistributedLockClient));

        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Transient, descriptor!.Lifetime);
    }

    [Fact]
    public void AddDaprDistributedLock_Singleton_ReturnsSameInstance()
    {
        var services = new ServiceCollection();
        services.AddDaprDistributedLock(lifetime: ServiceLifetime.Singleton);

        var serviceProvider = services.BuildServiceProvider();
        var client1 = serviceProvider.GetService<DaprDistributedLockClient>();
        var client2 = serviceProvider.GetService<DaprDistributedLockClient>();

        Assert.NotNull(client1);
        Assert.NotNull(client2);
        Assert.Same(client1, client2);
    }

    [Fact]
    public async Task AddDaprDistributedLock_Scoped_ReturnsDifferentInstancesAcrossScopes()
    {
        var services = new ServiceCollection();
        services.AddDaprDistributedLock(lifetime: ServiceLifetime.Scoped);

        var serviceProvider = services.BuildServiceProvider();

        await using var scope1 = serviceProvider.CreateAsyncScope();
        var client1 = scope1.ServiceProvider.GetService<DaprDistributedLockClient>();

        await using var scope2 = serviceProvider.CreateAsyncScope();
        var client2 = scope2.ServiceProvider.GetService<DaprDistributedLockClient>();

        Assert.NotNull(client1);
        Assert.NotNull(client2);
        Assert.NotSame(client1, client2);
    }

    [Fact]
    public void AddDaprDistributedLock_Transient_ReturnsDifferentInstances()
    {
        var services = new ServiceCollection();
        services.AddDaprDistributedLock(lifetime: ServiceLifetime.Transient);

        var serviceProvider = services.BuildServiceProvider();
        var client1 = serviceProvider.GetService<DaprDistributedLockClient>();
        var client2 = serviceProvider.GetService<DaprDistributedLockClient>();

        Assert.NotNull(client1);
        Assert.NotNull(client2);
        Assert.NotSame(client1, client2);
    }

    [Fact]
    public void AddDaprDistributedLock_WithConfigure_PassesBuilderToAction()
    {
        const string apiToken = "test-token";
        var services = new ServiceCollection();
        services.AddDaprDistributedLock((_, builder) => builder.UseDaprApiToken(apiToken));

        var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetService<DaprDistributedLockClient>() as DaprDistributedLockGrpcClient;

        Assert.NotNull(client);
        Assert.Equal(apiToken, client!.DaprApiToken);
    }

    [Fact]
    public void AddDaprDistributedLock_ReturnsIDaprDistributedLockBuilder()
    {
        var services = new ServiceCollection();
        var builder = services.AddDaprDistributedLock();

        Assert.IsAssignableFrom<IDaprDistributedLockBuilder>(builder);
    }

    [Fact]
    public void AddDaprDistributedLock_ReturnedBuilder_HasServicesReference()
    {
        var services = new ServiceCollection();
        var builder = services.AddDaprDistributedLock();

        Assert.Same(services, builder.Services);
    }
}
