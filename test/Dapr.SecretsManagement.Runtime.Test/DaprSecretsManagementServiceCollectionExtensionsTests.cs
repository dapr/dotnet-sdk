// ------------------------------------------------------------------------
//  Copyright 2026 The Dapr Authors
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

using Dapr.SecretsManagement.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.SecretsManagement.Test;

public sealed class DaprSecretsManagementServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDaprSecretsManagementClient_RegistersClientInServiceCollection()
    {
        var services = new ServiceCollection();
        var builder = services.AddDaprSecretsManagementClient();

        Assert.NotNull(builder);
        Assert.IsAssignableFrom<IDaprSecretsManagementBuilder>(builder);

        var descriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(DaprSecretsManagementClient));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddDaprSecretsManagementClient_RespectsCustomLifetime()
    {
        var services = new ServiceCollection();
        services.AddDaprSecretsManagementClient(lifetime: ServiceLifetime.Transient);

        var descriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(DaprSecretsManagementClient));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddDaprSecretsManagementClient_BuilderExposesSameServices()
    {
        var services = new ServiceCollection();
        var builder = services.AddDaprSecretsManagementClient();

        Assert.Same(services, builder.Services);
    }

    [Fact]
    public void AddDaprSecretsManagementClient_ScopedLifetimeRegistersCorrectly()
    {
        var services = new ServiceCollection();
        services.AddDaprSecretsManagementClient(lifetime: ServiceLifetime.Scoped);

        var descriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(DaprSecretsManagementClient));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddDaprSecretsManagementClient_ConfigureCallbackIsAccepted()
    {
        var services = new ServiceCollection();
        var callbackInvoked = false;

        services.AddDaprSecretsManagementClient((sp, builder) =>
        {
            callbackInvoked = true;
            builder.UseGrpcEndpoint("http://custom-host:50001");
        });

        // The callback is deferred until resolution, so just verify registration succeeds.
        var descriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(DaprSecretsManagementClient));
        Assert.NotNull(descriptor);
        // callbackInvoked will be false here since it's only called during resolution.
        Assert.False(callbackInvoked);
    }

    [Fact]
    public void AddDaprSecretsManagementClient_ResolvesClientFromServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddDaprSecretsManagementClient();

        using var provider = services.BuildServiceProvider();
        var client = provider.GetService<DaprSecretsManagementClient>();

        Assert.NotNull(client);
        Assert.IsType<DaprSecretsManagementGrpcClient>(client);
    }

    [Fact]
    public void AddDaprSecretsManagementClient_ConfigureCallbackIsInvokedDuringResolution()
    {
        var services = new ServiceCollection();
        var callbackInvoked = false;

        services.AddDaprSecretsManagementClient((sp, builder) =>
        {
            callbackInvoked = true;
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetService<DaprSecretsManagementClient>();

        Assert.NotNull(client);
        Assert.True(callbackInvoked);
    }

    [Fact]
    public void AddDaprSecretsManagementClient_MultipleRegistrationsDoNotConflict()
    {
        var services = new ServiceCollection();
        services.AddDaprSecretsManagementClient();
        services.AddDaprSecretsManagementClient();

        // Should have registrations, and resolution should not throw.
        using var provider = services.BuildServiceProvider();
        var client = provider.GetService<DaprSecretsManagementClient>();
        Assert.NotNull(client);
    }
}
