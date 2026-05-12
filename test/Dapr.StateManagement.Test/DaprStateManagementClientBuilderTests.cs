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

using System;
using System.Linq;
using System.Text.Json;
using Dapr.StateManagement;
using Dapr.StateManagement.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.StateManagement.Test;

public class DaprStateManagementClientBuilderTests
{
    [Fact]
    public void Build_WithDefaults_ReturnsClient()
    {
        var builder = new DaprStateManagementClientBuilder();
        using var client = builder.Build();
        Assert.NotNull(client);
    }

    [Fact]
    public void Build_WithCustomGrpcEndpoint_Succeeds()
    {
        var builder = new DaprStateManagementClientBuilder();
        builder.UseGrpcEndpoint("http://localhost:50001");
        using var client = builder.Build();
        Assert.NotNull(client);
    }

    [Fact]
    public void Build_WithCustomJsonOptions_Succeeds()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var builder = new DaprStateManagementClientBuilder();
        builder.UseJsonSerializationOptions(options);
        using var client = builder.Build();
        Assert.NotNull(client);
    }

    [Fact]
    public void Build_WithApiToken_Succeeds()
    {
        var builder = new DaprStateManagementClientBuilder();
        builder.UseDaprApiToken("test-token");
        using var client = builder.Build();
        Assert.NotNull(client);
    }

    [Fact]
    public void AddDaprStateManagementClient_RegistersClientAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddDaprStateManagementClient();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(DaprStateManagementClient));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddDaprStateManagementClient_WithCustomLifetime_RegistersCorrectly()
    {
        var services = new ServiceCollection();
        services.AddDaprStateManagementClient(lifetime: ServiceLifetime.Transient);

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(DaprStateManagementClient));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddDaprStateManagementClient_ReturnsBuilder()
    {
        var services = new ServiceCollection();
        var builder = services.AddDaprStateManagementClient();
        Assert.NotNull(builder);
        Assert.Same(services, builder.Services);
    }

    [Fact]
    public void AddDaprStateManagementClient_WithNullServices_Throws()
    {
        IServiceCollection services = null!;
        Assert.Throws<ArgumentNullException>(() => services.AddDaprStateManagementClient());
    }
}
