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
    using Dapr.E2E.Test.Actors.Reminders;
    using Xunit;

    public partial class E2ETests : IAsyncLifetime
    {
        [Fact]
        public async Task ActorCanStartAndStopReminder()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var proxy = this.ProxyFactory.CreateActorProxy<IReminderActor>(ActorId.CreateRandom(), "ReminderActor");

            await WaitForActorRuntimeAsync(proxy, cts.Token);

            // Start reminder, to count up to 10
            await proxy.StartReminder(new StartReminderOptions(){ Total = 10, });

            State state; 
            while (true)
            {
                cts.Token.ThrowIfCancellationRequested();

                state = await proxy.GetState();
                this.Output.WriteLine($"Got Count: {state.Count} IsReminderRunning: {state.IsReminderRunning}");
                if (!state.IsReminderRunning)
                {
                    break;
                }
            }

            // Should count up to exactly 10
            Assert.Equal(10, state.Count);
        }
    }
}
