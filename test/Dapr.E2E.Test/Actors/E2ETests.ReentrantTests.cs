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
    using Dapr.E2E.Test.Actors.Reentrancy;
    using Xunit;

    public partial class E2ETests : IAsyncLifetime
    {
        private static readonly int NUM_CALLS = 10;

        [Fact]
        public async Task ActorCanPerformReentrantCalls()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var proxy = this.ProxyFactory.CreateActorProxy<IReentrantActor>(ActorId.CreateRandom(), "ReentrantActor");

            await WaitForActorRuntimeAsync(proxy, cts.Token);

            await proxy.ReentrantCall(new ReentrantCallOptions(){ CallsRemaining = NUM_CALLS, });
            var state = await proxy.GetState();
            var records = state.Records;

            Assert.True(records.Count == NUM_CALLS * 2);
            for (int i = 0; i < NUM_CALLS * 2; i++)
            {
                Assert.Equal(i < NUM_CALLS, records[i].IsEnter);

                if (i > 0)
                {
                    Assert.True(records[i].Timestamp > records[i - 1].Timestamp);
                }
            }
        }
    }
}
