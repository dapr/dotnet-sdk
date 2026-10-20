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

using Dapr.Search.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Search.Test;

public sealed class DaprSearchClientBuilderTests
{
    [Fact]
    public void BuildsStandaloneClient()
    {
        using var client = new DaprSearchClientBuilder().UseGrpcEndpoint("http://localhost:50001").Build();
        Assert.IsType<DaprSearchGrpcClient>(client);
    }

    [Theory]
    [InlineData("ftp://localhost:50001")]
    [InlineData("file:///tmp/endpoint")]
    public void RejectsNonHttpEndpoints(string endpoint)
    {
        Assert.Throws<InvalidOperationException>(() => new DaprSearchClientBuilder().UseGrpcEndpoint(endpoint).Build());
        Assert.Throws<InvalidOperationException>(() => new DaprSearchClientBuilder().UseHttpEndpoint(endpoint).Build());
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public async Task RegistersConfiguredLifetimeAndDisposesClients(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DAPR_API_TOKEN"] = "configuration-token" })
            .Build());
        var callbacks = 0;
        var registration = services.AddDaprSearchClient((provider, builder) =>
        {
            Assert.NotNull(provider.GetRequiredService<IConfiguration>());
            Assert.Equal("configuration-token", builder.DaprApiToken);
            builder.UseDaprApiToken("override-token").UseGrpcEndpoint("http://localhost:50001");
            callbacks++;
        }, lifetime);
        Assert.Same(services, registration.Services);
        using var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        var first = scope.ServiceProvider.GetRequiredService<DaprSearchClient>();
        var second = scope.ServiceProvider.GetRequiredService<DaprSearchClient>();
        if (lifetime == ServiceLifetime.Transient)
            Assert.NotSame(first, second);
        else
            Assert.Same(first, second);
        using var otherScope = provider.CreateScope();
        var third = otherScope.ServiceProvider.GetRequiredService<DaprSearchClient>();
        if (lifetime == ServiceLifetime.Singleton)
            Assert.Same(first, third);
        else
            Assert.NotSame(first, third);
        Assert.Equal(lifetime == ServiceLifetime.Singleton ? 1 : lifetime == ServiceLifetime.Scoped ? 2 : 3, callbacks);
        scope.Dispose();
        provider.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            first.ListIndexesAsync("store", cancellationToken: TestContext.Current.CancellationToken));
    }
}
