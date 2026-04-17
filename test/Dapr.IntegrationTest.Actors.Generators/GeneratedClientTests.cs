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
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.IntegrationTest.Actors.Generators.Actors;
using Dapr.IntegrationTest.Actors.Generators.Infrastructure;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Harnesses;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Actors.Generators;

/// <summary>
/// Integration tests that verify the source-generated actor client (<c>ClientActorClient</c>)
/// can correctly invoke actor methods on a real Dapr sidecar and actor runtime.
/// These tests replicate and extend the coverage of the E2E test suite in
/// <c>Dapr.E2E.Test.Actors.Generators</c>.
/// </summary>
public sealed class GeneratedClientTests
{
    /// <summary>
    /// Verifies that the generated client can retrieve the default state from the remote actor.
    /// This is equivalent to the first half of the E2E <c>TestGeneratedClientAsync</c> test.
    /// </summary>
    [Fact]
    public async Task GeneratedClient_CanGetDefaultState()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(useJsonSerialization: true, cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();

        var actorId = ActorId.CreateRandom();
        const string actorType = "RemoteActor";

        // Use the strongly-typed proxy to wait for the runtime to be ready.
        var pingProxy = proxyFactory.CreateActorProxy<IRemoteActor>(actorId, actorType);
        await ActorRuntimeHelper.WaitForActorRuntimeAsync(pingProxy, cts.Token);

        // Create the generated client through the weakly-typed proxy.
        var actorProxy = proxyFactory.Create(actorId, actorType);
        var client = new ClientActorClient(actorProxy);

        var state = await client.GetStateAsync(cts.Token);

        Assert.NotNull(state);
        Assert.Equal("default", state.Value);
    }

    /// <summary>
    /// Verifies that the generated client can set and then retrieve state from the remote actor.
    /// This is equivalent to the E2E <c>TestGeneratedClientAsync</c> test.
    /// </summary>
    [Fact]
    public async Task GeneratedClient_CanSetAndGetState()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(useJsonSerialization: true, cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();

        var actorId = ActorId.CreateRandom();
        const string actorType = "RemoteActor";

        var pingProxy = proxyFactory.CreateActorProxy<IRemoteActor>(actorId, actorType);
        await ActorRuntimeHelper.WaitForActorRuntimeAsync(pingProxy, cts.Token);

        var actorProxy = proxyFactory.Create(actorId, actorType);
        var client = new ClientActorClient(actorProxy);

        await client.SetStateAsync(new ClientState("updated state"), cts.Token);

        var state = await client.GetStateAsync(cts.Token);
        Assert.Equal("updated state", state.Value);
    }

    /// <summary>
    /// Verifies that the generated client correctly maps method names via <see cref="Dapr.Actors.Generators.ActorMethodAttribute"/>
    /// when the server-side method has a different name from the client-side async method.
    /// </summary>
    [Fact]
    public async Task GeneratedClient_RespectsActorMethodNameMapping()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(useJsonSerialization: true, cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();

        var actorId = ActorId.CreateRandom();
        const string actorType = "RemoteActor";

        var pingProxy = proxyFactory.CreateActorProxy<IRemoteActor>(actorId, actorType);
        await ActorRuntimeHelper.WaitForActorRuntimeAsync(pingProxy, cts.Token);

        var actorProxy = proxyFactory.Create(actorId, actorType);
        var client = new ClientActorClient(actorProxy);

        // SayHelloAsync on the client maps to SayHello on the server via [ActorMethod(Name = "SayHello")]
        var result = await client.SayHelloAsync("World", cts.Token);
        Assert.Equal("Hello, World!", result);
    }

    /// <summary>
    /// Verifies that the generated client can invoke a void (fire-and-forget) method
    /// with no parameters via the <c>IncrementCallCountAsync</c> method.
    /// </summary>
    [Fact]
    public async Task GeneratedClient_CanInvokeVoidMethodWithNoParameters()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(useJsonSerialization: true, cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();

        var actorId = ActorId.CreateRandom();
        const string actorType = "RemoteActor";

        var pingProxy = proxyFactory.CreateActorProxy<IRemoteActor>(actorId, actorType);
        await ActorRuntimeHelper.WaitForActorRuntimeAsync(pingProxy, cts.Token);

        var actorProxy = proxyFactory.Create(actorId, actorType);
        var client = new ClientActorClient(actorProxy);

        // Call the void method multiple times.
        await client.IncrementCallCountAsync(cts.Token);
        await client.IncrementCallCountAsync(cts.Token);
        await client.IncrementCallCountAsync(cts.Token);

        // Verify the side-effect via GetCallCount.
        var count = await client.GetCallCountAsync(cts.Token);
        Assert.Equal(3, count);
    }

    /// <summary>
    /// Verifies that multiple state updates through the generated client are correctly
    /// reflected, confirming that the last write wins.
    /// </summary>
    [Fact]
    public async Task GeneratedClient_MultipleStateUpdatesReflectLastWrite()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(useJsonSerialization: true, cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();

        var actorId = ActorId.CreateRandom();
        const string actorType = "RemoteActor";

        var pingProxy = proxyFactory.CreateActorProxy<IRemoteActor>(actorId, actorType);
        await ActorRuntimeHelper.WaitForActorRuntimeAsync(pingProxy, cts.Token);

        var actorProxy = proxyFactory.Create(actorId, actorType);
        var client = new ClientActorClient(actorProxy);

        await client.SetStateAsync(new ClientState("first"), cts.Token);
        await client.SetStateAsync(new ClientState("second"), cts.Token);
        await client.SetStateAsync(new ClientState("third"), cts.Token);

        var state = await client.GetStateAsync(cts.Token);
        Assert.Equal("third", state.Value);
    }

    /// <summary>
    /// Verifies that two generated clients pointing at different actor IDs maintain
    /// independent state — a write to one does not affect the other.
    /// </summary>
    [Fact]
    public async Task GeneratedClient_DifferentActorIdsHaveIndependentState()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(useJsonSerialization: true, cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();

        const string actorType = "RemoteActor";

        var actorId1 = ActorId.CreateRandom();
        var actorId2 = ActorId.CreateRandom();

        var pingProxy = proxyFactory.CreateActorProxy<IRemoteActor>(actorId1, actorType);
        await ActorRuntimeHelper.WaitForActorRuntimeAsync(pingProxy, cts.Token);

        var client1 = new ClientActorClient(proxyFactory.Create(actorId1, actorType));
        var client2 = new ClientActorClient(proxyFactory.Create(actorId2, actorType));

        await client1.SetStateAsync(new ClientState("state-for-actor-1"), cts.Token);
        await client2.SetStateAsync(new ClientState("state-for-actor-2"), cts.Token);

        var state1 = await client1.GetStateAsync(cts.Token);
        var state2 = await client2.GetStateAsync(cts.Token);

        Assert.Equal("state-for-actor-1", state1.Value);
        Assert.Equal("state-for-actor-2", state2.Value);
    }

    /// <summary>
    /// Verifies that the generated client correctly handles cancellation tokens by
    /// passing them through to the actor proxy without errors under normal operation.
    /// </summary>
    [Fact]
    public async Task GeneratedClient_SupportsCancellationToken()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(useJsonSerialization: true, cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();

        var actorId = ActorId.CreateRandom();
        const string actorType = "RemoteActor";

        var pingProxy = proxyFactory.CreateActorProxy<IRemoteActor>(actorId, actorType);
        await ActorRuntimeHelper.WaitForActorRuntimeAsync(pingProxy, cts.Token);

        var actorProxy = proxyFactory.Create(actorId, actorType);
        var client = new ClientActorClient(actorProxy);

        // Create a separate cancellation token to ensure it's properly threaded through.
        using var methodCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);

        await client.SetStateAsync(new ClientState("cancellation-test"), methodCts.Token);
        var state = await client.GetStateAsync(methodCts.Token);

        Assert.Equal("cancellation-test", state.Value);
    }

    /// <summary>
    /// Verifies that the generated client works with JSON serialization enabled,
    /// confirming that complex state objects are correctly serialized and deserialized.
    /// </summary>
    [Fact]
    public async Task GeneratedClient_WorksWithJsonSerialization()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(useJsonSerialization: true, cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();

        var actorId = ActorId.CreateRandom();
        const string actorType = "RemoteActor";

        var pingProxy = proxyFactory.CreateActorProxy<IRemoteActor>(actorId, actorType);
        await ActorRuntimeHelper.WaitForActorRuntimeAsync(pingProxy, cts.Token);

        var actorProxy = proxyFactory.Create(actorId, actorType);
        var client = new ClientActorClient(actorProxy);

        // Set a state with special characters to verify JSON handling.
        var specialState = new ClientState("value with \"quotes\" and special chars: <>&");
        await client.SetStateAsync(specialState, cts.Token);

        var retrieved = await client.GetStateAsync(cts.Token);
        Assert.Equal(specialState.Value, retrieved.Value);
    }

    // ------------------------------------------------------------------
    // Test infrastructure helpers
    // ------------------------------------------------------------------

    private static async Task<ActorTestContext> CreateTestAppAsync(
        bool useJsonSerialization,
        CancellationToken cancellationToken)
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("actor-generators-components");

        var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
            needsActorState: true,
            cancellationToken: cancellationToken);
        await environment.StartAsync(cancellationToken);

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildActors();

        var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddActors(options =>
                {
                    options.UseJsonSerialization = useJsonSerialization;
                    options.Actors.RegisterActor<RemoteActor>();
                });
            })
            .ConfigureApp(app =>
            {
                app.MapActorsHandlers();
            })
            .BuildAndStartAsync();
        return new ActorTestContext(environment, testApp);
    }
}
