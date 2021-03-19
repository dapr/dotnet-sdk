// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------
namespace Dapr.E2E.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.Actors;
    using Dapr.E2E.Test.Actors.Timers;
    using Xunit;

    public partial class E2ETests : IAsyncLifetime
    {
        [Fact]
        public async Task ActorCanStartAndStopTimer()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var proxy = this.ProxyFactory.CreateActorProxy<ITimerActor>(ActorId.CreateRandom(), "TimerActor");

            await WaitForActorRuntimeAsync(proxy, cts.Token);

            // Start timer, to count up to 10
            await proxy.StartTimer(new StartTimerOptions(){ Total = 10, });

            State state; 
            while (true)
            {
                cts.Token.ThrowIfCancellationRequested();

                state = await proxy.GetState();
                this.Output.WriteLine($"Got Count: {state.Count} IsTimerRunning: {state.IsTimerRunning}");
                if (!state.IsTimerRunning)
                {
                    break;
                }
            }

            // Should count up to exactly 10
            Assert.Equal(10, state.Count);
        }
    }
}
