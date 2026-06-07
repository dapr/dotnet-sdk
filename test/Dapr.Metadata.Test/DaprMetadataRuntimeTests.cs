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

using Dapr.Metadata.Abstractions;
using Dapr.Metadata.Extensions;
using Dapr.Metadata.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Dapr.Metadata.Test;

public sealed class DaprMetadataRuntimeTests
{
    [Fact]
    public void RefreshSignal_NotifyChanged_FiresCurrentTokenAndReplacesIt()
    {
        var signal = new DaprMetadataRefreshSignal();
        var firstToken = signal.GetChangeToken();
        var secondTokenBeforeNotify = signal.GetChangeToken();
        var firstChanged = false;
        using var registration = firstToken.RegisterChangeCallback(_ => firstChanged = true, null);

        signal.NotifyChanged();
        var secondToken = signal.GetChangeToken();

        Assert.True(firstChanged);
        Assert.True(firstToken.HasChanged);
        Assert.True(secondTokenBeforeNotify.HasChanged);
        Assert.False(secondToken.HasChanged);
        Assert.NotSame(firstToken, secondToken);
    }

    [Fact]
    public void ChangeTokenSource_ReturnsDefaultNameAndSignalToken()
    {
        var signal = new DaprMetadataRefreshSignal();
        var source = new DaprMetadataChangeTokenSource(signal);

        var token = source.GetChangeToken();
        signal.NotifyChanged();

        Assert.Equal(Options.DefaultName, source.Name);
        Assert.True(token.HasChanged);
    }

    [Fact]
    public void OptionsFactory_Create_ReturnsProviderMetadata()
    {
        var expected = new DaprMetadata { AppId = "orders" };
        var provider = new FakeMetadataProvider(expected);
        var factory = new DaprMetadataOptionsFactory(
            provider,
            Enumerable.Empty<IConfigureOptions<DaprMetadata>>(),
            Enumerable.Empty<IPostConfigureOptions<DaprMetadata>>(),
            Enumerable.Empty<IValidateOptions<DaprMetadata>>());

        var actual = factory.Create(Options.DefaultName);

        Assert.Same(expected, actual);
        Assert.Equal(1, provider.GetCallCount);
    }

    [Fact]
    public async Task Warmup_StartAsync_FetchesMetadata()
    {
        var provider = new FakeMetadataProvider(new DaprMetadata { AppId = "orders" });
        var warmup = new DaprMetadataWarmup(provider);

        await warmup.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, provider.GetCallCount);
        Assert.Equal(TestContext.Current.CancellationToken, provider.LastCancellationToken);
    }

    [Fact]
    public async Task Warmup_StopAsync_Completes()
    {
        var provider = new FakeMetadataProvider(new DaprMetadata());
        var warmup = new DaprMetadataWarmup(provider);

        await warmup.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, provider.GetCallCount);
        Assert.Equal(0, provider.RefreshCallCount);
    }

    [Fact]
    public void AddDaprMetadata_RegistersRuntimeServices()
    {
        var services = new ServiceCollection();

        var returned = services.AddDaprMetadata();

        Assert.Same(services, returned);
        Assert.Contains(services, d => d.ServiceType == typeof(DaprMetadataRefreshSignal) && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, d => d.ServiceType == typeof(IDaprMetadataProvider) && d.ImplementationType == typeof(DaprMetadataProvider) && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, d => d.ServiceType == typeof(IOptionsFactory<DaprMetadata>) && d.ImplementationType == typeof(DaprMetadataOptionsFactory) && d.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, d => d.ServiceType == typeof(IOptionsChangeTokenSource<DaprMetadata>) && d.ImplementationType == typeof(DaprMetadataChangeTokenSource) && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(DaprMetadataWarmup) && d.Lifetime == ServiceLifetime.Singleton);
    }

    private sealed class FakeMetadataProvider(DaprMetadata metadata) : IDaprMetadataProvider
    {
        public int GetCallCount { get; private set; }

        public int RefreshCallCount { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<DaprMetadata> GetAsync(CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            LastCancellationToken = cancellationToken;
            return new ValueTask<DaprMetadata>(metadata);
        }

        public ValueTask<DaprMetadata> RefreshAsync(CancellationToken cancellationToken = default)
        {
            RefreshCallCount++;
            LastCancellationToken = cancellationToken;
            return new ValueTask<DaprMetadata>(metadata);
        }
    }
}
