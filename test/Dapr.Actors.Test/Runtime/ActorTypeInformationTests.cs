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

namespace Dapr.Actors.Test;

using System;
using System.Reflection;
using Dapr.Actors;
using Dapr.Actors.Runtime;
using Xunit;

public sealed class ActorTypeInformationTests
{
    private const string RenamedActorTypeName = "MyRenamedActor";
    private const string ParamActorTypeName = "AnotherRenamedActor";

    private interface ITestActor : IActor
    {
    }

    [Fact]
    public void TestInferredActorType()
    {
        var actorType = typeof(TestActor);
        var actorTypeInformation = ActorTypeInformation.Get(actorType, actorTypeName: null);

        Assert.Equal(actorType.Name, actorTypeInformation.ActorTypeName);
    }

    [Fact]
    public void TestExplicitActorType()
    {
        var actorType = typeof(RenamedActor);

        Assert.NotEqual(RenamedActorTypeName, actorType.Name);

        var actorTypeInformation = ActorTypeInformation.Get(actorType, actorTypeName: null);

        Assert.Equal(RenamedActorTypeName, actorTypeInformation.ActorTypeName);
    }

    [Theory]
    [InlineData(typeof(TestActor))]
    [InlineData(typeof(RenamedActor))]
    public void TestExplicitActorTypeAsParam(Type actorType)
    {
        Assert.NotEqual(ParamActorTypeName, actorType.Name);
        Assert.NotEqual(ParamActorTypeName, actorType.GetCustomAttribute<ActorAttribute>()?.TypeName);

        var actorTypeInformation = ActorTypeInformation.Get(actorType, ParamActorTypeName);

        Assert.Equal(ParamActorTypeName, actorTypeInformation.ActorTypeName);
    }

    private sealed class TestActor : Actor, ITestActor
    {
        public TestActor(ActorHost host)
            : base(host)
        {
        }
    }

    [Actor(TypeName = RenamedActorTypeName)]
    private sealed class RenamedActor : Actor, ITestActor
    {
        public RenamedActor(ActorHost host)
            : base(host)
        {
        }
    }
}