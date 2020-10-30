// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Test.Runtime
{
    using System;
    using System.Threading;
    using Dapr.Actors.Runtime;
    using FluentAssertions;
    using Moq;
    using Xunit;
    using Microsoft.Extensions.Logging;
    using System.Linq;

    public sealed class ActorRuntimeOptionsTests
    {
        [Fact]
        public void TestRegisterActor_SavesActorServiceFactory()
        {
            var actorType = typeof(TestActor);
            var actorTypeInformation = ActorTypeInformation.Get(actorType);
            var actorService = new ActorService(actorTypeInformation, new LoggerFactory());
            Func<ActorTypeInformation, ActorService> actorServiceFactory = (actorTypeInfo) => NewActorServiceFactory(actorTypeInfo);

            Func<ActorService, ActorId, TestActor> actorFactory = (service, id) =>
                new TestActor(service, id, null);

            var actorRuntimeOptions = new ActorRuntimeOptions();
            actorRuntimeOptions.RegisterActor<TestActor>(actorServiceFactory);
            Assert.True(actorRuntimeOptions.actorServicesFunc.Count.Equals(1));
            var key = actorRuntimeOptions.actorServicesFunc.Keys.First();
            Assert.True(key.ActorTypeName.Equals(ActorTypeInformation.Get(actorType).ActorTypeName));
        }

        private ActorService NewActorServiceFactory(ActorTypeInformation actorTypeInfo)
        {
            return new ActorService(actorTypeInfo, null, null);
        }
    }
}