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
namespace Dapr.E2E.Test;

using System;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors;
using Dapr.E2E.Test.Actors.WeaklyTypedTesting;
using Shouldly;
using Xunit;

public partial class E2ETests : IAsyncLifetime
{
#if NET8_0_OR_GREATER
    [Fact]
    public async Task WeaklyTypedActorCanReturnPolymorphicResponse()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var pingProxy = this.ProxyFactory.CreateActorProxy<IWeaklyTypedTestingActor>(ActorId.CreateRandom(), "WeaklyTypedTestingActor");
        var proxy = this.ProxyFactory.Create(ActorId.CreateRandom(), "WeaklyTypedTestingActor");

        await WaitForActorRuntimeAsync(pingProxy, cts.Token);

        var result = await proxy.InvokeMethodAsync<ResponseBase>(nameof(IWeaklyTypedTestingActor.GetPolymorphicResponse));

        result.ShouldBeOfType<DerivedResponse>().DerivedProperty.ShouldNotBeNullOrWhiteSpace();
    }
#else
        [Fact]
        public async Task WeaklyTypedActorCanReturnDerivedResponse()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var pingProxy = this.ProxyFactory.CreateActorProxy<IWeaklyTypedTestingActor>(ActorId.CreateRandom(), "WeaklyTypedTestingActor");
            var proxy = this.ProxyFactory.Create(ActorId.CreateRandom(), "WeaklyTypedTestingActor");

            await WaitForActorRuntimeAsync(pingProxy, cts.Token);

            var result = await proxy.InvokeMethodAsync<DerivedResponse>(nameof(IWeaklyTypedTestingActor.GetPolymorphicResponse));

            result.ShouldBeOfType<DerivedResponse>().DerivedProperty.ShouldNotBeNullOrWhiteSpace();
        }
#endif
    [Fact]
    public async Task WeaklyTypedActorCanReturnNullResponse()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var pingProxy = this.ProxyFactory.CreateActorProxy<IWeaklyTypedTestingActor>(ActorId.CreateRandom(), "WeaklyTypedTestingActor");
        var proxy = this.ProxyFactory.Create(ActorId.CreateRandom(), "WeaklyTypedTestingActor");

        await WaitForActorRuntimeAsync(pingProxy, cts.Token);

        var result = await proxy.InvokeMethodAsync<ResponseBase>(nameof(IWeaklyTypedTestingActor.GetNullResponse));

        result.ShouldBeNull();
    }
}