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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.E2E.Test.Actors.Reentrancy;
using Xunit;
using Xunit.Abstractions;

public class ReentrantTests : DaprTestAppLifecycle
{
    private static readonly int NumCalls = 10;
    private readonly Lazy<IActorProxyFactory> proxyFactory;
    private IActorProxyFactory ProxyFactory => this.HttpEndpoint == null ? null : this.proxyFactory.Value;

    public ReentrantTests(ITestOutputHelper output, DaprTestAppFixture fixture) : base(output, fixture)
    {
        base.Configuration = new DaprRunConfiguration
        {
            UseAppPort = true,
            AppId = "reentrantapp",
            TargetProject = "./../../../../../test/Dapr.E2E.Test.App.ReentrantActor/Dapr.E2E.Test.App.ReentrantActors.csproj",
        };

        this.proxyFactory = new Lazy<IActorProxyFactory>(() =>
        {
            Debug.Assert(this.HttpEndpoint != null);
            return new ActorProxyFactory(new ActorProxyOptions() { HttpEndpoint = this.HttpEndpoint, });
        });
    }

    [Fact]
    public async Task ActorCanPerformReentrantCalls()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var proxy = this.ProxyFactory.CreateActorProxy<IReentrantActor>(ActorId.CreateRandom(), "ReentrantActor");

        await ActorRuntimeChecker.WaitForActorRuntimeAsync(this.AppId, this.Output, proxy, cts.Token);

        await proxy.ReentrantCall(new ReentrantCallOptions(){ CallsRemaining = NumCalls, });
        var records = new List<CallRecord>();
        for (int i = 0; i < NumCalls; i++)
        {
            var state = await proxy.GetState(i);
            records.AddRange(state.Records);
        }

        var enterRecords  = records.FindAll(record => record.IsEnter);
        var exitRecords  = records.FindAll(record => !record.IsEnter);
            
        this.Output.WriteLine($"Got {records.Count} records.");
        Assert.True(records.Count == NumCalls * 2);
        for (int i = 0; i < NumCalls; i++)
        {
            for (int j = 0; j < NumCalls; j++)
            {
                // Assert all the enters happen before the exits.
                Assert.True(enterRecords[i].Timestamp < exitRecords[j].Timestamp);
            }
        }
    }
}