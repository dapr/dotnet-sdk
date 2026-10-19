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

using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Abstractions.Registry;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Abstractions.State.Versioning;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Actors.Next.Core.State;
using Microsoft.Extensions.DependencyInjection;
using ActorClient = Dapr.Actors.Next.Abstractions.Attributes.GenerateActorClientAttribute;
using Host = Dapr.Actors.Next.Abstractions.Attributes.DaprActorAttribute;

namespace Dapr.IntegrationTest.Actors.Next;

public sealed class GeneratorOptimizationIntegrationTests
{
    [Fact]
    public async Task Partial_multi_interface_actor_dispatches_shared_and_distinct_methods()
    {
        await using var provider = CreateProvider();
        var factory = provider.GetRequiredService<IActorProxyFactory>();
        var id = new ActorId("optimized-contracts");
        var writer = factory.Create<IOptimizedWallet>(id, "OptimizedWallet");
        var reader = factory.Create<IOptimizedWalletReader>(id, "OptimizedWallet");

        Assert.Equal("0", (await reader.ReadAsync()).Text);
        await writer.AddAsync(10, 20);
        Assert.Equal("30", (await writer.ReadAsync()).Text);
        Assert.Equal("30", (await reader.ReadAsync()).Text);
        Assert.Equal(60, await reader.MultiplyAsync());

        var registry = provider.GetRequiredService<IActorRegistry>();
        Assert.True(registry.TryGet("OptimizedWallet", out var descriptor));
        Assert.Equal(new[] { "AddAsync", "MultiplyAsync", "ReadAsync" }, descriptor.Methods.Select(method => method.Name));
        var multiply = Assert.Single(descriptor.Methods, method => method.Name == "MultiplyAsync");
        Assert.True(multiply.Parameters[0].HasDefaultValue);
        Assert.True(multiply.Parameters[1].IsCancellationToken);

        await provider.GetRequiredService<IActorRuntime>().DeactivateAsync("OptimizedWallet", id);
        Assert.Equal("30", (await reader.ReadAsync()).Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Explicit_and_additive_migrations_preserve_data_hashes_and_persisted_discriminators(bool startAtExplicitTarget)
    {
        await using var provider = CreateProvider();
        var migrator = provider.GetRequiredService<IActorStateMigrator>();
        var serializer = provider.GetRequiredService<IActorWireSerializer>();
        var store = provider.GetRequiredService<IActorStateStore>();
        var target = migrator.ResolveTargetNode(typeof(OptimizedBalanceV3))!;
        var from = migrator.ResolveTargetNode(startAtExplicitTarget ? typeof(OptimizedBalanceV2) : typeof(OptimizedBalance))!;
        var id = new ActorId(startAtExplicitTarget ? "optimized-additive" : "optimized-explicit");
        var bytes = startAtExplicitTarget
            ? await migrator.WriteEnvelopedAsync(new OptimizedBalanceV2("42"), from, serializer)
            : await migrator.WriteEnvelopedAsync(new OptimizedBalance { Value = 42 }, from, serializer);
        await store.WriteAsync("OptimizedWallet", id.Value, "balance", bytes);

        var actor = provider.GetRequiredService<IActorProxyFactory>().Create<IOptimizedWalletReader>(id, "OptimizedWallet");
        var result = await actor.ReadAsync();
        Assert.Equal("42", result.Text);
        Assert.False(result.Frozen);
        var persisted = (await store.ReadAsync("OptimizedWallet", id.Value, "balance"))!.Value;
        var envelope = serializer.DeserializeFromBytes<ActorStateEnvelope<OptimizedBalanceV3>>(persisted)!;
        Assert.Equal(target.Index, envelope.Discriminator.ChainIndex);
        Assert.Equal(target.ShapeHash, envelope.Discriminator.ShapeHash);
        Assert.Equal("42", envelope.Value.Text);

        await provider.GetRequiredService<IActorRuntime>().DeactivateAsync("OptimizedWallet", id);
        Assert.Equal("42", (await actor.ReadAsync()).Text);
    }

    private static ServiceProvider CreateProvider()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(OptimizedWalletActor).Module.ModuleHandle);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDaprActors(options => options.EnableSidecarTransport = false);
        return services.BuildServiceProvider(validateScopes: true);
    }
}

public interface IOptimizedWalletRead : IActor
{
    Task<OptimizedBalanceV3> ReadAsync(CancellationToken cancellationToken = default);
}

[ActorClient]
public partial interface IOptimizedWallet : IOptimizedWalletRead
{
    Task AddAsync(int left, int right, CancellationToken cancellationToken = default);
}

public partial interface IOptimizedWallet;

[ActorClient]
public interface IOptimizedWalletReader : IOptimizedWalletRead
{
    ValueTask<int> MultiplyAsync(int factor = 2, CancellationToken cancellationToken = default);
}

[Host("OptimizedWallet")]
public sealed partial class OptimizedWalletActor(ActorActivationContext context) : Actor, IOptimizedWallet, IOptimizedWalletReader
{
    protected override ActorId Id => context.ActorId;
    protected override IActorStateAccessor State => context.State;

    public async Task<OptimizedBalanceV3> ReadAsync(CancellationToken cancellationToken = default) =>
        (await State.GetOrCreateAsync("balance", () => new OptimizedBalanceV3 { Text = "0" }, cancellationToken)).Value;

    public async Task AddAsync(int left, int right, CancellationToken cancellationToken = default)
    {
        var state = await State.GetOrCreateAsync("balance", () => new OptimizedBalanceV3 { Text = "0" }, cancellationToken);
        state.Value.Text = (int.Parse(state.Value.Text, CultureInfo.InvariantCulture) + left + right).ToString(CultureInfo.InvariantCulture);
    }

    public async ValueTask<int> MultiplyAsync(int factor = 2, CancellationToken cancellationToken = default) =>
        int.Parse((await ReadAsync(cancellationToken)).Text, CultureInfo.InvariantCulture) * factor;
}

public sealed partial class OptimizedWalletActor : IOptimizedWalletReader;

public sealed partial class OptimizedBalance
{
    public int Value { get; set; }
}

public sealed partial class OptimizedBalance;

public sealed record OptimizedBalanceV2(string Text);

public sealed class OptimizedBalanceV3
{
    public string Text { get; set; } = "";
    public bool Frozen { get; set; }
}

public sealed partial class OptimizedBalanceUpcaster : IActorStateUpcaster<OptimizedBalance, OptimizedBalanceV2>
{
    public ValueTask<OptimizedBalanceV2> UpcastAsync(OptimizedBalance state, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new OptimizedBalanceV2(state.Value.ToString(CultureInfo.InvariantCulture)));
}

public sealed partial class OptimizedBalanceUpcaster : IActorStateUpcaster<OptimizedBalance, OptimizedBalanceV2>;
