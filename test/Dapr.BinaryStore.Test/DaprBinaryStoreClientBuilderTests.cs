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

#nullable enable

using System;
using System.Linq;
using Dapr.BinaryStore.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.BinaryStore.Test;

public class DaprBinaryStoreClientBuilderTests
{
    [Fact]
    public void Build_WithDefaults_ReturnsClient()
    {
        using var client = new DaprBinaryStoreClientBuilder().Build();
        Assert.NotNull(client);
    }

    [Fact]
    public void Build_WithCustomGrpcEndpoint_Succeeds()
    {
        var builder = new DaprBinaryStoreClientBuilder();
        builder.UseGrpcEndpoint("http://localhost:50001");
        using var client = builder.Build();
        Assert.NotNull(client);
    }

    [Fact]
    public void Build_WithApiToken_Succeeds()
    {
        var builder = new DaprBinaryStoreClientBuilder();
        builder.UseDaprApiToken("test-token");
        using var client = builder.Build();
        Assert.NotNull(client);
    }

    [Fact]
    public void AddDaprBinaryStoreClient_RegistersClientAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddDaprBinaryStoreClient();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(DaprBinaryStoreClient));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddDaprBinaryStoreClient_WithCustomLifetime_RegistersCorrectly()
    {
        var services = new ServiceCollection();
        services.AddDaprBinaryStoreClient(lifetime: ServiceLifetime.Transient);

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(DaprBinaryStoreClient));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddDaprBinaryStoreClient_ReturnsBuilder()
    {
        var services = new ServiceCollection();
        var builder = services.AddDaprBinaryStoreClient();
        Assert.Same(services, builder.Services);
    }

    [Fact]
    public void AddDaprBinaryStoreClient_WithNullServices_Throws()
    {
        IServiceCollection services = null!;
        Assert.Throws<ArgumentNullException>(() => services.AddDaprBinaryStoreClient());
    }
}
