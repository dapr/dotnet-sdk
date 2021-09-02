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
    using Dapr.Actors.Client;
    using Dapr.E2E.Test.Actors.Timeout;
    using Xunit;

    public partial class E2ETests : IAsyncLifetime
    {
        [Fact]
        public async Task ActorTimeoutInProxyIsRespected()
        {
            // Default timeout.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var proxy = this.ProxyFactory.CreateActorProxy<ITimeoutActor>(ActorId.CreateRandom(), "TimeoutActor");

            await WaitForActorRuntimeAsync(proxy, cts.Token);

            // This call should succeed.
            await proxy.TimeoutCall(new TimeoutCallOptions(){ SleepTime = 5 });
            
            // Get a new proxy with a lower timeout.
            var timeoutProxy = this.ProxyFactory.CreateActorProxy<ITimeoutActor>(ActorId.CreateRandom(), "TimeoutActor", new ActorProxyOptions { RequestTimeout = TimeSpan.FromSeconds(1) });
            await WaitForActorRuntimeAsync(timeoutProxy, cts.Token);
            try
            {
                await timeoutProxy.TimeoutCall(new TimeoutCallOptions() { SleepTime = 5 });
                Assert.False(true, "Request should have timed out.");
            }
            catch (Exception)
            {
                // Expected.
            }
        }
    }
}
