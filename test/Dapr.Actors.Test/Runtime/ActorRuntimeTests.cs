// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Test
{
    using System;
    using Dapr.Actors;
    using Dapr.Actors.Runtime;
    using Xunit;

    public sealed class ActorRuntimeTests
    {
        private interface ITestActor : IActor
        {
        }

        [Fact]
        public void TestInferredActorType()
        {
            var actorRuntime = new ActorRuntime();

            string actorTypeName = typeof(TestActor).Name;

            Assert.DoesNotContain(actorTypeName, actorRuntime.RegisteredActorTypes, StringComparer.InvariantCulture);

            actorRuntime.RegisterActor<TestActor>();

            Assert.Contains(actorTypeName, actorRuntime.RegisteredActorTypes, StringComparer.InvariantCulture);
        }

        [Fact]
        public void TestExplicitActorType()
        {
            var actorRuntime = new ActorRuntime();

            string actorTypeName = "MyTestActor";

            Assert.NotEqual(actorTypeName, typeof(TestActor).Name);
            Assert.DoesNotContain(actorTypeName, actorRuntime.RegisteredActorTypes, StringComparer.InvariantCulture);

            actorRuntime.RegisterActor<TestActor>(actorTypeName);

            Assert.Contains(actorTypeName, actorRuntime.RegisteredActorTypes, StringComparer.InvariantCulture);
        }

        private sealed class TestActor : Actor, ITestActor
        {
            public TestActor(ActorService actorService, ActorId actorId)
                : base(actorService, actorId)
            {
            }
        }
    }
}