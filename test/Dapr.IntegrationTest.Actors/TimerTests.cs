// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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
using Dapr.IntegrationTest.Actors.Timers;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Harnesses;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Actors;

/// <summary>
/// Integration tests that verify Dapr actor timer registration, firing, and expiry.
/// </summary>
public sealed class TimerTests
{
    /// <summary>
    /// Verifies that a timer fires the expected number of times before self-cancelling.
    /// </summary>
    [Fact]
    public async Task ActorCanStartAndStopTimer()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var proxy = proxyFactory.CreateActorProxy<ITimerActor>(ActorId.CreateRandom(), "TimerActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        await proxy.StartTimer(new StartTimerOptions { Total = 10 });

        TimerState state;
        while (true)
        {
            cts.Token.ThrowIfCancellationRequested();
            state = await proxy.GetState();
            if (!state.IsTimerRunning) break;
        }

        Assert.Equal(10, state.Count);
    }

    /// <summary>
    /// Verifies that a timer configured with a TTL stops firing after the TTL elapses.
    /// </summary>
    [Fact]
    public async Task ActorCanStartTimerWithTtl()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var proxy = proxyFactory.CreateActorProxy<ITimerActor>(ActorId.CreateRandom(), "TimerActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        await proxy.StartTimerWithTtl(TimeSpan.FromSeconds(2));
        var start = DateTime.Now;

        await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);

        var state = await proxy.GetState();
        Assert.True(state.Timestamp.Subtract(start) > TimeSpan.Zero, "Timer may not have fired.");
        Assert.True(DateTime.Now.Subtract(state.Timestamp) > TimeSpan.FromSeconds(1),
            $"Timer fired too recently. {DateTime.Now} - {state.Timestamp}");
    }

    // ------------------------------------------------------------------
    // Test infrastructure helpers
    // ------------------------------------------------------------------

    private static async Task<Dapr.Testcontainers.Common.Testing.DaprTestApplication> CreateTestAppAsync(
        CancellationToken cancellationToken)
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("actor-timer-components");

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
                    options.Actors.RegisterActor<TimerActor>();
                });
            })
            .ConfigureApp(app =>
            {
                app.MapActorsHandlers();
            })
            .BuildAndStartAsync();
    }
}
