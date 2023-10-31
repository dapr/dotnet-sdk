// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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
namespace Dapr.E2E.Test
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.Actors;
    using Dapr.E2E.Test.Actors.State;
    using Xunit;

    public partial class E2ETests : IAsyncLifetime
    {
        [Fact]
        public async Task ActorCanSaveStateWithTTL()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var proxy = this.ProxyFactory.CreateActorProxy<IStateActor>(ActorId.CreateRandom(), "StateActor");

            await WaitForActorRuntimeAsync(proxy, cts.Token);

            await proxy.SetState("key", "value", TimeSpan.FromSeconds(2));

            var resp = await proxy.GetState("key");
            Assert.Equal("value", resp);

            await Task.Delay(TimeSpan.FromSeconds(2));

            resp = await proxy.GetState("key");
            Assert.Null(resp);

            await proxy.SetState("key", "new-value", null);
            resp = await proxy.GetState("key");
            Assert.Equal("new-value", resp);
        }
    }
}
