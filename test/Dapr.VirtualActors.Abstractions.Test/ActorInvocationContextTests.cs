// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

using Shouldly;
using Xunit;

namespace Dapr.VirtualActors.Abstractions.Test;

public class ActorInvocationContextTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var actorId = new VirtualActorId("test");
        var context = new ActorInvocationContext("MyActor", actorId, "DoWork", ActorCallType.ActorMethod);

        context.ActorType.ShouldBe("MyActor");
        context.ActorId.ShouldBe(actorId);
        context.MethodName.ShouldBe("DoWork");
        context.CallType.ShouldBe(ActorCallType.ActorMethod);
    }

    [Fact]
    public void Properties_AreInitiallyEmpty()
    {
        var context = new ActorInvocationContext("T", new VirtualActorId("1"), "M", ActorCallType.ActorMethod);
        context.Properties.ShouldBeEmpty();
    }

    [Fact]
    public void Properties_CanStoreAndRetrieveData()
    {
        var context = new ActorInvocationContext("T", new VirtualActorId("1"), "M", ActorCallType.ActorMethod);
        context.Properties["key"] = "value";
        context.Properties["key"].ShouldBe("value");
    }

    [Fact]
    public void RequestData_IsNullByDefault()
    {
        var context = new ActorInvocationContext("T", new VirtualActorId("1"), "M", ActorCallType.ActorMethod);
        context.RequestData.ShouldBeNull();
        context.ResponseData.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidActorType_Throws(string? actorType)
    {
        Should.Throw<ArgumentException>(() =>
            new ActorInvocationContext(actorType!, new VirtualActorId("1"), "M", ActorCallType.ActorMethod));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidMethodName_Throws(string? methodName)
    {
        Should.Throw<ArgumentException>(() =>
            new ActorInvocationContext("T", new VirtualActorId("1"), methodName!, ActorCallType.ActorMethod));
    }
}
