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

using System;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors;
using Dapr.E2E.Test.Actors.ErrorTesting;
using Xunit;

namespace Dapr.E2E.Test;

public partial class E2ETests : IAsyncLifetime
{
    [Fact]
    public async Task ActorSuccessfullyClearsStateAfterErrorWithRemoting()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var proxy = this.ProxyFactory.CreateActorProxy<IRegression762Actor>(ActorId.CreateRandom(), "Regression762Actor");

        await WaitForActorRuntimeAsync(proxy, cts.Token);

        var key = Guid.NewGuid().ToString();
        var throwingCall = new StateCall
        {
            Key = key,
            Value = "Throw value",
            Operation = "ThrowException"
        };

        var setCall = new StateCall()
        {
            Key = key,
            Value = "Real value",
            Operation = "SetState"
        };

        var savingCall = new StateCall()
        {
            Operation = "SaveState"
        };

        // We attempt to delete it on the unlikely chance it's already there.
        await proxy.RemoveState(throwingCall.Key);

        // Initiate a call that will set the state, then throw.
        await Assert.ThrowsAsync<ActorMethodInvocationException>(async () => await proxy.SaveState(throwingCall));

        // Save the state and assert that the old value was not persisted.
        await proxy.SaveState(savingCall);
        var errorResp = await proxy.GetState(key);
        Assert.Equal(string.Empty, errorResp);

        // Persist normally and ensure it works.
        await proxy.SaveState(setCall);
        var resp = await proxy.GetState(key);
        Assert.Equal("Real value", resp);
    }

    [Fact]
    public async Task ActorSuccessfullyClearsStateAfterErrorWithoutRemoting()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var pingProxy = this.ProxyFactory.CreateActorProxy<IRegression762Actor>(ActorId.CreateRandom(), "Regression762Actor");
        var proxy = this.ProxyFactory.Create(ActorId.CreateRandom(), "Regression762Actor");

        await WaitForActorRuntimeAsync(pingProxy, cts.Token);

        var key = Guid.NewGuid().ToString();
        var throwingCall = new StateCall
        {
            Key = key,
            Value = "Throw value",
            Operation = "ThrowException"
        };

        var setCall = new StateCall()
        {
            Key = key,
            Value = "Real value",
            Operation = "SetState"
        };

        var savingCall = new StateCall()
        {
            Operation = "SaveState"
        };

        // We attempt to delete it on the unlikely chance it's already there.
        await proxy.InvokeMethodAsync("RemoveState", throwingCall.Key);

        // Initiate a call that will set the state, then throw.
        await Assert.ThrowsAsync<DaprApiException>(async () => await proxy.InvokeMethodAsync("SaveState", throwingCall));
                
        // Save the state and assert that the old value was not persisted.
        await proxy.InvokeMethodAsync("SaveState", savingCall);
        var errorResp = await proxy.InvokeMethodAsync<string, string>("GetState", key);
        Assert.Equal(string.Empty, errorResp);

        // Persist normally and ensure it works.
        await proxy.InvokeMethodAsync("SaveState", setCall);
        var resp = await proxy.InvokeMethodAsync<string, string>("GetState", key);
        Assert.Equal("Real value", resp);
    }
}