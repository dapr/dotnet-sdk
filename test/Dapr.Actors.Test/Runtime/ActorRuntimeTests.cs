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
        private const string RenamedActorTypeName = "MyRenamedActor";

        private interface ITestActor : IActor
        {
        }

        [Fact]
        public void TestInferredActorType()
        {
            var actorType = typeof(TestActor);
            var actorRuntime = new ActorRuntime();

            Assert.Empty(actorRuntime.RegisteredActorTypes);

            actorRuntime.RegisterActor<TestActor>();

            Assert.Contains(actorType.Name, actorRuntime.RegisteredActorTypes, StringComparer.InvariantCulture);
        }

        [Fact]
        public void TestExplicitActorType()
        {
            var actorType = typeof(RenamedActor);
            var actorRuntime = new ActorRuntime();

            Assert.NotEqual(RenamedActorTypeName, actorType.Name);

            Assert.Empty(actorRuntime.RegisteredActorTypes);

            actorRuntime.RegisterActor<RenamedActor>();

            Assert.Contains(RenamedActorTypeName, actorRuntime.RegisteredActorTypes, StringComparer.InvariantCulture);
        }

        [Fact]
        public void TestRegisterOtherTypeAtRuntime()
        {
            var actorType = typeof(ActorRuntimeTests);
            var actorRuntime = new ActorRuntime();

            actorRuntime.RegisterActor(actorType);

            Assert.DoesNotContain(actorType.Name, actorRuntime.RegisteredActorTypes, StringComparer.InvariantCulture);
        }

        private sealed class TestActor : Actor, ITestActor
        {
            public TestActor(ActorService actorService, ActorId actorId)
                : base(actorService, actorId)
            {
            }
        }

        [Actor(TypeName = RenamedActorTypeName)]
        private sealed class RenamedActor : Actor, ITestActor
        {
            public RenamedActor(ActorService actorService, ActorId actorId)
                : base(actorService, actorId)
            {
            }
        }
    }
}