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
using Dapr.IntegrationTest.Actors.Infrastructure;
using Dapr.IntegrationTest.Actors.Regression;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Harnesses;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Actors;

/// <summary>
/// Checks turn isolation through a real sidecar, including after the caller cancels its HTTP request.
/// </summary>
/// <remarks>
/// Set DAPR_RUNTIME_VERSION to compare sidecar releases. Host-side turn isolation must hold
/// even when the sidecar releases its lock on cancellation; uncancelled cases are controls.
/// </remarks>
public sealed class TurnBasedCancellationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, false, true, false)]
    [InlineData(true, true, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, true, false, true)]
    [InlineData(false, true, false, true)]
    public async Task ActorTurnsMustNotOverlapWhenCallerCancels(bool useRemoting, bool cancelFirstCall,
        bool enableReentrancy, bool cooperativeCancellation)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        using var firstCallCancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        var probe = new TurnBasedCancellationProbe { CooperateWithCancellation = cooperativeCancellation };
        var options = new DaprRuntimeOptions("1.18.4");
        output.WriteLine(
            $"Dapr runtime image tag: {options.Version}; remoting: {useRemoting}; cancel: {cancelFirstCall}; reentrancy: {enableReentrancy}; cooperative: {cooperativeCancellation}");

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
            options: options, needsActorState: true, cancellationToken: timeout.Token);
        await environment.StartAsync(timeout.Token);

        var componentsDir = TestDirectoryManager.CreateTestDirectory("actor-turn-cancellation-components");
        var harness = new DaprHarnessBuilder(componentsDir)
            .WithOptions(options)
            .WithEnvironment(environment)
            .BuildActors();

        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddSingleton(probe);
                builder.Services.AddActors(actorOptions =>
                {
                    actorOptions.ReentrancyConfig = new ActorReentrancyConfig { Enabled = enableReentrancy };
                    actorOptions.Actors.RegisterActor<TurnBasedCancellationActor>();
                });
            })
            .ConfigureApp(app => app.MapActorsHandlers())
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var actorId = ActorId.CreateRandom();
        var typedProxy = proxyFactory.CreateActorProxy<ITurnBasedCancellationActor>(
            actorId, nameof(TurnBasedCancellationActor));
        var weakProxy = proxyFactory.Create(actorId, nameof(TurnBasedCancellationActor));
        await ActorRuntimeHelper.WaitForActorRuntimeAsync(typedProxy, timeout.Token);

        Task HoldTurnAsync(CancellationToken cancellationToken) => useRemoting
            ? typedProxy.HoldTurn(cancellationToken)
            : weakProxy.InvokeMethodAsync(nameof(ITurnBasedCancellationActor.HoldTurn), cancellationToken);

        Task ObserveTurnAsync(CancellationToken cancellationToken) => useRemoting
            ? typedProxy.ObserveTurn(cancellationToken)
            : weakProxy.InvokeMethodAsync(nameof(ITurnBasedCancellationActor.ObserveTurn), cancellationToken);

        Task? firstCall = null;
        Task? secondCall = null;
        try
        {
            firstCall = HoldTurnAsync(firstCallCancellation.Token);
            var firstInstance = await probe.FirstEntered.Task.WaitAsync(timeout.Token);
            secondCall = ObserveTurnAsync(timeout.Token);

            if (cancelFirstCall)
            {
                await firstCallCancellation.CancelAsync();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstCall.WaitAsync(timeout.Token));
                Assert.False(probe.FirstFinished.Task.IsCompleted);
            }

            // Give the second request time to reach the sidecar while the first turn is held.
            // A correct runtime queues it; a broken runtime lets it enter and records the overlap.
            await Task.WhenAny(probe.SecondEntered.Task, Task.Delay(TimeSpan.FromSeconds(3), timeout.Token));
            output.WriteLine($"Host cancellation observed: {probe.HostCancellationObserved.Task.IsCompleted}");
            probe.ReleaseFirst.TrySetResult();

            await probe.FirstFinished.Task.WaitAsync(timeout.Token);
            await secondCall.WaitAsync(timeout.Token);
            var secondEntry = await probe.SecondEntered.Task.WaitAsync(timeout.Token);

            Assert.Same(firstInstance, secondEntry.Instance);
            Assert.True(secondEntry.FirstFinished,
                $"Actor turns overlapped (remoting={useRemoting}, cancelFirstCall={cancelFirstCall}, reentrancy={enableReentrancy}): " +
                "the second method entered the same actor object before the first method finished.");
        }
        finally
        {
            probe.ReleaseFirst.TrySetResult();
            // Drain actor work before disposing the host, even when an assertion fails.
            if (firstCall is not null && !firstCall.IsCanceled)
            {
                await firstCall.WaitAsync(timeout.Token);
            }

            if (probe.FirstEntered.Task.IsCompleted)
            {
                await probe.FirstFinished.Task.WaitAsync(timeout.Token);
            }

            if (secondCall is not null)
            {
                await secondCall.WaitAsync(timeout.Token);
            }
        }
    }
}
