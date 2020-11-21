// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Test
{
    using Dapr.Actors;
    using Dapr.Actors.Runtime;
    using Xunit;

    public sealed class ActorTypeInformationTests
    {
        private const string RenamedActorTypeName = "MyRenamedActor";

        private interface ITestActor : IActor
        {
        }

        [Fact]
        public void TestInferredActorType()
        {
            var actorType = typeof(TestActor);
            var actorTypeInformation = ActorTypeInformation.Get(actorType);

            Assert.Equal(actorType.Name, actorTypeInformation.ActorTypeName);
        }

        [Fact]
        public void TestExplicitActorType()
        {
            var actorType = typeof(RenamedActor);

            Assert.NotEqual(RenamedActorTypeName, actorType.Name);

            var actorTypeInformation = ActorTypeInformation.Get(actorType);

            Assert.Equal(RenamedActorTypeName, actorTypeInformation.ActorTypeName);
        }

        private sealed class TestActor : Actor, ITestActor
        {
            public TestActor(ActorHost actorService)
                : base(actorService)
            {
            }
        }

        [Actor(TypeName = RenamedActorTypeName)]
        private sealed class RenamedActor : Actor, ITestActor
        {
            public RenamedActor(ActorHost actorService)
                : base(actorService)
            {
            }
        }
    }
}
