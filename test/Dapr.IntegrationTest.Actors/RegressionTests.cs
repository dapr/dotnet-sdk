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
using Dapr.IntegrationTest.Actors.Regression;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Harnesses;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Actors;

/// <summary>
/// Integration tests that reproduce regressions to prevent reintroduction of fixed bugs.
/// </summary>
public sealed class RegressionTests
{
    /// <summary>
    /// Regression test for GitHub issue #762: an exception thrown mid-method must not persist
    /// state changes made prior to the exception when using actor remoting.
    /// </summary>
    [Fact]
    public async Task ActorSuccessfullyClearsStateAfterErrorWithRemoting()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var proxy = proxyFactory.CreateActorProxy<IRegressionActor>(ActorId.CreateRandom(), "RegressionActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        var key = Guid.NewGuid().ToString();
        var throwingCall = new StateCall { Key = key, Value = "Throw value", Operation = "ThrowException" };
        var setCall = new StateCall { Key = key, Value = "Real value", Operation = "SetState" };
        var savingCall = new StateCall { Operation = "SaveState" };

        await proxy.RemoveState(key);

        // A call that sets state then throws – the state must be rolled back.
        await Assert.ThrowsAsync<ActorMethodInvocationException>(() => proxy.SaveState(throwingCall));

        // SaveState without setting a value – nothing should be persisted from the failed call.
        await proxy.SaveState(savingCall);
        var errorResp = await proxy.GetState(key);
        Assert.Equal(string.Empty, errorResp);

        // Normal set + save – state should now be persisted.
        await proxy.SaveState(setCall);
        var resp = await proxy.GetState(key);
        Assert.Equal("Real value", resp);
    }

    /// <summary>
    /// Regression test for GitHub issue #762 exercised through the weakly-typed (non-remoting) proxy path.
    /// </summary>
    [Fact]
    public async Task ActorSuccessfullyClearsStateAfterErrorWithoutRemoting()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var pingProxy = proxyFactory.CreateActorProxy<IRegressionActor>(ActorId.CreateRandom(), "RegressionActor");
        var proxy = proxyFactory.Create(ActorId.CreateRandom(), "RegressionActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(pingProxy, cts.Token);

        var key = Guid.NewGuid().ToString();
        var throwingCall = new StateCall { Key = key, Value = "Throw value", Operation = "ThrowException" };
        var setCall = new StateCall { Key = key, Value = "Real value", Operation = "SetState" };
        var savingCall = new StateCall { Operation = "SaveState" };

        await proxy.InvokeMethodAsync("RemoveState", key, cts.Token);

        // A weakly-typed call that sets state then throws – the state must be rolled back.
        await Assert.ThrowsAsync<DaprApiException>(
            () => proxy.InvokeMethodAsync("SaveState", throwingCall, cts.Token));

        await proxy.InvokeMethodAsync("SaveState", savingCall, cts.Token);
        var errorResp = await proxy.InvokeMethodAsync<string, string>("GetState", key, cts.Token);
        Assert.Equal(string.Empty, errorResp);

        await proxy.InvokeMethodAsync("SaveState", setCall, cts.Token);
        var resp = await proxy.InvokeMethodAsync<string, string>("GetState", key, cts.Token);
        Assert.Equal("Real value", resp);
    }

    // ------------------------------------------------------------------
    // Test infrastructure helpers
    // ------------------------------------------------------------------

    private static async Task<Dapr.Testcontainers.Common.Testing.DaprTestApplication> CreateTestAppAsync(
        CancellationToken cancellationToken)
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("actor-regression-components");

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
                    options.Actors.RegisterActor<RegressionActor>();
                });
            })
            .ConfigureApp(app =>
            {
                app.MapActorsHandlers();
            })
            .BuildAndStartAsync();
    }
}
