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

    public sealed class ActorTests
    {
        [Fact]
        public void TestNewActorWithMockStateManager()
        {
            var mockStateManager = new Mock<IActorStateManager>();
            var testDemoActor = this.CreateTestDemoActor(mockStateManager.Object);
            testDemoActor.ActorService.Should().NotBeNull();
            testDemoActor.IsDirty.Should().BeFalse();
            testDemoActor.Id.Should().NotBeNull();
        }

        [Fact]
        public void TestSaveState()
        {
            var mockStateManager = new Mock<IActorStateManager>();
            mockStateManager.Setup(manager => manager.SaveStateAsync(It.IsAny<CancellationToken>()));
            var testDemoActor = this.CreateTestDemoActor(mockStateManager.Object);
            testDemoActor.SaveTestState();
            mockStateManager.Verify(manager => manager.SaveStateAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void TestResetStateAsync()
        {
            var mockStateManager = new Mock<IActorStateManager>();
            mockStateManager.Setup(manager => manager.ClearCacheAsync(It.IsAny<CancellationToken>()));
            var testDemoActor = this.CreateTestDemoActor(mockStateManager.Object);
            testDemoActor.ResetTestStateAsync();
            mockStateManager.Verify(manager => manager.ClearCacheAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// On my test code I want to pass the mock statemanager all the time.
        /// </summary>
        /// <param name="actorStateManager">Mock StateManager.</param>
        /// <returns>TestActor.</returns>
        private TestActor CreateTestDemoActor(IActorStateManager actorStateManager)
        {
            var actorTypeInformation = ActorTypeInformation.Get(typeof(TestActor));
            Func<ActorService, ActorId, TestActor> actorFactory = (service, id) =>
                new TestActor(service, id, actorStateManager);
            var actorService = new ActorService(actorTypeInformation, actorFactory);
            var testActor = actorFactory.Invoke(actorService, ActorId.CreateRandom());
            return testActor;
        }
    }
}