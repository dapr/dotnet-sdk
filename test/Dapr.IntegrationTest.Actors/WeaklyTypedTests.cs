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
using Dapr.IntegrationTest.Actors.Infrastructure;
using Dapr.IntegrationTest.Actors.WeaklyTyped;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Harnesses;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Actors;

/// <summary>
/// Integration tests that verify weakly-typed actor invocation, including polymorphic
/// response deserialization and null response handling.
/// </summary>
public sealed class WeaklyTypedTests
{
    /// <summary>
    /// Verifies that a weakly-typed actor proxy can return and correctly deserialize a
    /// <see cref="DerivedResponse"/> when the declared return type is <see cref="ResponseBase"/>.
    /// </summary>
    [Fact]
    public async Task WeaklyTypedActorCanReturnPolymorphicResponse()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();

        var pingProxy = proxyFactory.CreateActorProxy<IWeaklyTypedTestingActor>(
            ActorId.CreateRandom(), "WeaklyTypedTestingActor");
        var proxy = proxyFactory.Create(ActorId.CreateRandom(), "WeaklyTypedTestingActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(pingProxy, cts.Token);

        var result = await proxy.InvokeMethodAsync<ResponseBase>(
            nameof(IWeaklyTypedTestingActor.GetPolymorphicResponse),
            cts.Token);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.BaseProperty));
    }

    /// <summary>
    /// Verifies that a weakly-typed actor proxy can return a null response without throwing.
    /// </summary>
    [Fact]
    public async Task WeaklyTypedActorCanReturnNullResponse()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();

        var pingProxy = proxyFactory.CreateActorProxy<IWeaklyTypedTestingActor>(
            ActorId.CreateRandom(), "WeaklyTypedTestingActor");
        var proxy = proxyFactory.Create(ActorId.CreateRandom(), "WeaklyTypedTestingActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(pingProxy, cts.Token);

        var result = await proxy.InvokeMethodAsync<ResponseBase?>(
            nameof(IWeaklyTypedTestingActor.GetNullResponse),
            cts.Token);

        Assert.Null(result);
    }

    // ------------------------------------------------------------------
    // Test infrastructure helpers
    // ------------------------------------------------------------------

    private static async Task<Dapr.Testcontainers.Common.Testing.DaprTestApplication> CreateTestAppAsync(
        CancellationToken cancellationToken)
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("actor-weaklytyped-components");

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
            needsActorState: true,
            cancellationToken: cancellationToken);
        await environment.StartAsync(cancellationToken);

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildActors();

        return await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddActors(options =>
                {
                    options.Actors.RegisterActor<WeaklyTypedTestingActor>();
                });
            })
            .ConfigureApp(app =>
            {
                app.MapActorsHandlers();
            })
            .BuildAndStartAsync();
    }
}
