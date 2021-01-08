// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Client
{
    using System;
    using Dapr.Actors.Builder;
    using Dapr.Actors.Client;
    using Dapr.Actors.Test;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Test class for Actor Code builder.
    /// </summary>
    public class ActorProxyTests
    {
        /// <summary>
        /// Tests Proxy Creation.
        /// </summary>
        [Fact]
        public void TestCreateActorProxy_Success()
        {
            var actorId = new ActorId("abc");
            var proxy = ActorProxy.Create(actorId, "TestActor");
            Assert.NotNull(proxy);
        }

        [Fact]
        public void TestCreateActorProxyThatImplementsInterface_Success()
        {
            var actorId = new ActorId("abc");
            var proxy = ActorProxy.Create(actorId, typeof(ITestActor), "TestActor");
            Assert.NotNull(proxy);
        }

        [Fact]
        public void TestCreateActorProxyThatImplementsInterface_NonSuccess()
        {
            var actorId = new ActorId("abc");
            Action action = () =>  ActorProxy.Create(actorId, typeof(ActorId), "TestActor");
            action.Should().Throw<ArgumentException>();
        }
    }
}
