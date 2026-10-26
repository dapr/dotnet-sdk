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

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.IntegrationTest.Actors.Activation;
using Dapr.IntegrationTest.Actors.Infrastructure;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Harnesses;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Actors;

public sealed class ActivationTests
{
    [Fact]
    public async Task CanActivateActorWithDependencyInjection()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var context = await CreateTestAppAsync(cts.Token);
        await WaitForRuntimeAsync(context, "dependency-injection", cts.Token);
        using var client = context.CreateHttpClient();

        Assert.Equal("0", await IncrementCounterAsync(client, "dependency-injection", cts.Token));
        Assert.Equal("1", await IncrementCounterAsync(client, "dependency-injection", cts.Token));
        await DeactivateActorAsync(client, "dependency-injection", cts.Token);
    }

    [Fact]
    public async Task InvokeMethod_UnregisteredActorType_ReturnsNonSuccessResponse()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var context = await CreateTestAppAsync(cts.Token);
        await WaitForRuntimeAsync(context, "registered-actor", cts.Token);
        using var client = context.CreateHttpClient();

        var response = await client.PutAsync(
            "/actors/NoSuchActorType/someId/method/SomeMethod",
            content: null,
            cts.Token);

        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task ConcurrentIncrement_SameActorId_RetainsConsistentState()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var context = await CreateTestAppAsync(cts.Token);
        const string actorId = "concurrent-actor";
        await WaitForRuntimeAsync(context, actorId, cts.Token);
        using var client = context.CreateHttpClient();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 5)
                .Select(_ => IncrementCounterAsync(client, actorId, cts.Token)));

        Assert.Equal(Enumerable.Range(0, 5), results.Select(int.Parse).OrderBy(value => value));
        await DeactivateActorAsync(client, actorId, cts.Token);
    }

    private static async Task WaitForRuntimeAsync(
        ActorTestContext context,
        string actorId,
        CancellationToken cancellationToken)
    {
        using var scope = context.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var proxy = proxyFactory.CreateActorProxy<IPingActor>(
            new ActorId(actorId),
            nameof(DependencyInjectionActor));
        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cancellationToken);
    }

    private static async Task<string> IncrementCounterAsync(
        HttpClient client,
        string actorId,
        CancellationToken cancellationToken)
    {
        var response = await client.PutAsync(
            $"/actors/{nameof(DependencyInjectionActor)}/{actorId}/method/{nameof(DependencyInjectionActor.IncrementAsync)}",
            content: null,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static async Task DeactivateActorAsync(
        HttpClient client,
        string actorId,
        CancellationToken cancellationToken)
    {
        var response = await client.DeleteAsync(
            $"/actors/{nameof(DependencyInjectionActor)}/{actorId}",
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<ActorTestContext> CreateTestAppAsync(CancellationToken cancellationToken)
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("actor-activation-components");
        var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
            needsActorState: true,
            cancellationToken: cancellationToken);
        await environment.StartAsync(cancellationToken);

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildActors();
        var app = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddScoped<CounterService>();
                builder.Services.AddActors(options =>
                    options.Actors.RegisterActor<DependencyInjectionActor>());
            })
            .ConfigureApp(webApp => webApp.MapActorsHandlers())
            .BuildAndStartAsync();

        return new ActorTestContext(environment, app);
    }
}
