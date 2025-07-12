// ------------------------------------------------------------------------
// Copyright 2022 The Dapr Authors
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

namespace Dapr.E2E.Test;

using System;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors;
using Dapr.E2E.Test.Actors.ExceptionTesting;
using Xunit;
public partial class E2ETests : IAsyncLifetime
{
    [Fact]
    public async Task ActorCanProvideExceptionDetails()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var proxy = this.ProxyFactory.CreateActorProxy<IExceptionActor>(ActorId.CreateRandom(), "ExceptionActor");
        await WaitForActorRuntimeAsync(proxy, cts.Token);
        ActorMethodInvocationException ex = await Assert.ThrowsAsync<ActorMethodInvocationException>(async () => await proxy.ExceptionExample());
        Assert.Contains("Remote Actor Method Exception", ex.Message);
        Assert.Contains("ExceptionExample", ex.Message);
        Assert.Contains("32", ex.Message);
    }
}