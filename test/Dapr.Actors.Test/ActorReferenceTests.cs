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
using System.Threading.Tasks;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;
using Dapr.Actors.Test;
using Xunit;

namespace Dapr.Actors;

public class ActorReferenceTests
{
    [Fact]
    public void Get_WhenActorIsNull_ReturnsNull()
    {
        // Arrange
        object actor = null;

        // Act
        var result = ActorReference.Get(actor);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Get_FromActorProxy_ReturnsActorReference()
    {
        // Arrange
        var expectedActorId = new ActorId("abc");
        var expectedActorType = "TestActor";
        var proxy = ActorProxy.Create(expectedActorId, typeof(ITestActor), expectedActorType);

        // Act
        var actorReference = ActorReference.Get(proxy);

        // Assert
        Assert.NotNull(actorReference);
        Assert.Equal(expectedActorId, actorReference.ActorId);
        Assert.Equal(expectedActorType, actorReference.ActorType);
    }

    [Fact]
    public async Task Get_FromActorImplementation_ReturnsActorReference()
    {
        // Arrange
        var expectedActorId = new ActorId("abc");
        var expectedActorType = nameof(ActorReferenceTestActor);
        var host = ActorHost.CreateForTest<ActorReferenceTestActor>(new ActorTestOptions() { ActorId = expectedActorId });
        var actor = new ActorReferenceTestActor(host);

        // Act
        var actorReference = await actor.GetActorReference();

        // Assert
        Assert.NotNull(actorReference);
        Assert.Equal(expectedActorId, actorReference.ActorId);
        Assert.Equal(expectedActorType, actorReference.ActorType);
    }

    [Fact]
    public void Get_WithInvalidObjectType_ThrowArgumentOutOfRangeException()
    {
        // Arrange
        var actor = new object();

        // Act
        var act = () => ActorReference.Get(actor);

        // Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(act);
        Assert.Equal("actor", exception.ParamName);
        Assert.Equal("Invalid actor object type. (Parameter 'actor')", exception.Message);
    }
}

public interface IActorReferenceTestActor : IActor
{
    Task<ActorReference> GetActorReference();
}

public class ActorReferenceTestActor : Actor, IActorReferenceTestActor
{
    public ActorReferenceTestActor(ActorHost host)
        : base(host)
    {
    }

    public Task<ActorReference> GetActorReference()
    {
        return Task.FromResult(ActorReference.Get(this));
    }
}
