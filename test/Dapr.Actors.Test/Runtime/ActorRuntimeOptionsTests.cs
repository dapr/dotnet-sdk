// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Test.Runtime
{
    using Dapr.Actors.Runtime;
    using Moq;
    using Xunit;
    using Microsoft.Extensions.Logging;

    public sealed class ActorRuntimeOptionsTests
    {
        [Fact]
        public void TestRegisterActor_SavesActivator()
        {
            var actorType = typeof(TestActor);
            var actorTypeInformation = ActorTypeInformation.Get(actorType);
            var host = new ActorHost(actorTypeInformation, ActorId.CreateRandom(), new LoggerFactory());
            var actor = new TestActor(host);

            var activator = Mock.Of<ActorActivator>();

            var actorRuntimeOptions = new ActorRuntimeOptions();
            actorRuntimeOptions.Actors.RegisterActor<TestActor>(registration =>
            {
                registration.Activator = activator;
            });

            Assert.Collection(
                actorRuntimeOptions.Actors,
                registration => 
                {
                    Assert.Same(actorTypeInformation.ImplementationType, registration.Type.ImplementationType);
                    Assert.Same(activator, registration.Activator);
                });
        }
    }
}
