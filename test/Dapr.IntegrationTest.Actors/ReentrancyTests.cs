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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;
using Dapr.IntegrationTest.Actors.Infrastructure;
using Dapr.IntegrationTest.Actors.Reentrancy;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Harnesses;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Actors;

/// <summary>
/// Integration tests that verify Dapr actor reentrancy: all enters must happen before any exits
/// in a recursively re-entering call chain.
/// </summary>
public sealed class ReentrancyTests
{
    private const int NumCalls = 10;

    /// <summary>
    /// Verifies that a reentrant actor can make <see cref="NumCalls"/> nested self-calls, and
    /// that the resulting enter/exit records confirm proper reentrant execution ordering.
    /// </summary>
    [Fact]
    public async Task ActorCanPerformReentrantCalls()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var proxy = proxyFactory.CreateActorProxy<IReentrantActor>(ActorId.CreateRandom(), "ReentrantActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        await proxy.ReentrantCall(new ReentrantCallOptions { CallsRemaining = NumCalls });

        var records = new List<CallRecord>();
        for (var i = 0; i < NumCalls; i++)
        {
            var state = await proxy.GetState(i);
            records.AddRange(state.Records);
        }

        var enterRecords = records.FindAll(r => r.IsEnter);
        var exitRecords = records.FindAll(r => !r.IsEnter);

        Assert.Equal(NumCalls * 2, records.Count);

        for (var i = 0; i < NumCalls; i++)
        for (var j = 0; j < NumCalls; j++)
        {
            // All enters must precede all exits.
            Assert.True(enterRecords[i].Timestamp < exitRecords[j].Timestamp,
                $"Enter record [{i}] did not precede exit record [{j}].");
        }
    }

    // ------------------------------------------------------------------
    // Test infrastructure helpers
    // ------------------------------------------------------------------

    private static async Task<Dapr.Testcontainers.Common.Testing.DaprTestApplication> CreateTestAppAsync(
        CancellationToken cancellationToken)
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("actor-reentrancy-components");

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
                    options.ReentrancyConfig = new ActorReentrancyConfig { Enabled = true };
                    options.Actors.RegisterActor<ReentrantActor>();
                });
            })
            .ConfigureApp(app =>
            {
                app.MapActorsHandlers();
            })
            .BuildAndStartAsync();
    }
}
