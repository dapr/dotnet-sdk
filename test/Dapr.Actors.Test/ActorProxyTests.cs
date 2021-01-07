// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Client
{
    using Dapr.Actors.Builder;
    using Dapr.Actors.Client;
    using Dapr.Actors.Test;
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
        public void TestCreateActorProxy()
        {
            var actorId = new ActorId("abc");
            var proxy1 = ActorProxy.Create(actorId, "TestActor");
            Assert.NotNull(proxy1);
            var proxy2 = ActorProxy.Create(actorId, typeof(ITestActor), "TestActor");
            Assert.NotNull(proxy2);
        }
    }
}
