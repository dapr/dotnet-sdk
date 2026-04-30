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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.IntegrationTest.Actors.Infrastructure;
using Dapr.IntegrationTest.Actors.Reminders;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Harnesses;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Actors;

/// <summary>
/// Integration tests that verify Dapr actor reminder registration, firing, and expiry.
/// </summary>
public sealed class ReminderTests
{
    /// <summary>
    /// Verifies that a reminder fires the expected number of times before self-cancelling.
    /// </summary>
    [Fact]
    public async Task ActorCanStartAndStopReminder()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var proxy = proxyFactory.CreateActorProxy<IReminderActor>(ActorId.CreateRandom(), "ReminderActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        await proxy.StartReminder(new StartReminderOptions { Total = 10 });

        ReminderState state;
        while (true)
        {
            cts.Token.ThrowIfCancellationRequested();
            state = await proxy.GetState();
            if (!state.IsReminderRunning) break;
        }

        Assert.Equal(10, state.Count);
    }

    /// <summary>
    /// Verifies that <c>GetReminder</c> returns <c>"null"</c> before the reminder is started,
    /// returns a valid reminder descriptor while it runs, and that exactly 10 invocations occur.
    /// </summary>
    [Fact]
    public async Task ActorCanStartAndStopAndGetReminder()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var proxy = proxyFactory.CreateActorProxy<IReminderActor>(ActorId.CreateRandom(), "ReminderActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        // Reminder not yet started – should return "null".
        var reminder = await proxy.GetReminder();
        Assert.Equal("null", reminder);

        await proxy.StartReminder(new StartReminderOptions { Total = 10 });

        var countGetReminder = 0;
        ReminderState state;
        while (true)
        {
            cts.Token.ThrowIfCancellationRequested();

            reminder = await proxy.GetReminder();
            Assert.NotNull(reminder);

            if (reminder != "null")
            {
                countGetReminder++;
                var reminderJson = JsonSerializer.Deserialize<JsonElement>(reminder);
                Assert.Equal("test-reminder", reminderJson.GetProperty("name").GetString());
                Assert.Equal(TimeSpan.FromMilliseconds(50).ToString(), reminderJson.GetProperty("period").GetString());
                Assert.Equal(TimeSpan.Zero.ToString(), reminderJson.GetProperty("dueTime").GetString());
            }

            state = await proxy.GetState();
            if (!state.IsReminderRunning) break;
        }

        Assert.Equal(10, state.Count);
        Assert.True(countGetReminder > 0, "GetReminder should have returned a non-null descriptor at least once.");
    }

    /// <summary>
    /// Verifies that a reminder configured with a repetition count fires exactly that many times.
    /// </summary>
    [Fact]
    public async Task ActorCanStartReminderWithRepetitions()
    {
        const int repetitions = 5;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var proxy = proxyFactory.CreateActorProxy<IReminderActor>(ActorId.CreateRandom(), "ReminderActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        await proxy.StartReminderWithRepetitions(repetitions);
        var start = DateTime.Now;

        await Task.Delay(TimeSpan.FromSeconds(7), cts.Token);

        var state = await proxy.GetState();
        Assert.Equal(repetitions, state.Count);
        Assert.True(state.Timestamp.Subtract(start) > TimeSpan.Zero, "Reminder may not have triggered.");
        Assert.True(DateTime.Now.Subtract(state.Timestamp) > TimeSpan.FromSeconds(1),
            $"Reminder triggered too recently. {DateTime.Now} - {state.Timestamp}");
    }

    /// <summary>
    /// Verifies that a reminder respects both TTL and repetition count, stopping at whichever limit is hit first.
    /// </summary>
    [Fact]
    public async Task ActorCanStartReminderWithTtlAndRepetitions()
    {
        const int repetitions = 2;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var proxy = proxyFactory.CreateActorProxy<IReminderActor>(ActorId.CreateRandom(), "ReminderActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        await proxy.StartReminderWithTtlAndRepetitions(TimeSpan.FromSeconds(5), repetitions);
        var start = DateTime.Now;

        await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);

        var state = await proxy.GetState();
        Assert.Equal(repetitions, state.Count);
        Assert.True(state.Timestamp.Subtract(start) > TimeSpan.Zero, "Reminder may not have triggered.");
        Assert.True(DateTime.Now.Subtract(state.Timestamp) > TimeSpan.FromSeconds(1),
            $"Reminder triggered too recently. {DateTime.Now} - {state.Timestamp}");
    }

    /// <summary>
    /// Verifies that a reminder configured with a TTL stops firing after the TTL elapses.
    /// </summary>
    [Fact]
    public async Task ActorCanStartReminderWithTtl()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var proxy = proxyFactory.CreateActorProxy<IReminderActor>(ActorId.CreateRandom(), "ReminderActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        await proxy.StartReminderWithTtl(TimeSpan.FromSeconds(2));
        var start = DateTime.Now;

        await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);

        var state = await proxy.GetState();
        Assert.True(state.Timestamp.Subtract(start) > TimeSpan.Zero, "Reminder may not have triggered.");
        Assert.True(DateTime.Now.Subtract(state.Timestamp) > TimeSpan.FromSeconds(1),
            $"Reminder triggered too recently. {DateTime.Now} - {state.Timestamp}");
    }

    // ------------------------------------------------------------------
    // Test infrastructure helpers
    // ------------------------------------------------------------------

    private static async Task<ActorTestContext> CreateTestAppAsync(
        CancellationToken cancellationToken)
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("actor-reminder-components");

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
                    options.Actors.RegisterActor<ReminderActor>();
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
