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

using System.Linq;
using System.Text.Json;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dapr.Actors.AspNetCore;

public class ActorHostingTest
{
    [Fact]
    public void CanRegisterActorsInSingleCalls()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddActors(options =>
        {
            options.Actors.RegisterActor<TestActor1>();
            options.Actors.RegisterActor<TestActor2>();
        });
            
        var runtime = services.BuildServiceProvider().GetRequiredService<ActorRuntime>();

        Assert.Collection(
            runtime.RegisteredActors.Select(r => r.Type.ActorTypeName).OrderBy(t => t),
            t => Assert.Equal(ActorTypeInformation.Get(typeof(TestActor1), actorTypeName: null).ActorTypeName, t),
            t => Assert.Equal(ActorTypeInformation.Get(typeof(TestActor2), actorTypeName: null).ActorTypeName, t));
    }

    [Fact]
    public void CanRegisterActorsInMultipleCalls()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddActors(options =>
        {
            options.Actors.RegisterActor<TestActor1>();
        });
            
        services.AddActors(options =>
        {
            options.Actors.RegisterActor<TestActor2>();
        });

        var runtime = services.BuildServiceProvider().GetRequiredService<ActorRuntime>();

        Assert.Collection(
            runtime.RegisteredActors.Select(r => r.Type.ActorTypeName).OrderBy(t => t),
            t => Assert.Equal(ActorTypeInformation.Get(typeof(TestActor1), actorTypeName: null).ActorTypeName, t),
            t => Assert.Equal(ActorTypeInformation.Get(typeof(TestActor2), actorTypeName: null).ActorTypeName, t));
    }

    [Fact]
    public void CanAccessProxyFactoryWithCustomJsonOptions()
    {
        var jsonOptions = new JsonSerializerOptions();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddActors(options =>
        {
            options.JsonSerializerOptions = jsonOptions;
        });
            
        services.AddActors(options =>
        {
            options.Actors.RegisterActor<TestActor2>();
        });

        var factory = (ActorProxyFactory)services.BuildServiceProvider().GetRequiredService<IActorProxyFactory>();
        Assert.Same(jsonOptions, factory.DefaultOptions.JsonSerializerOptions);
    }

    private interface ITestActor : IActor
    {
    }

    private class TestActor1 : Actor, ITestActor
    {
        public TestActor1(ActorHost host) 
            : base(host)
        {
        }
    }

    private class TestActor2 : Actor, ITestActor
    {
        public TestActor2(ActorHost host) 
            : base(host)
        {
        }
    }
}