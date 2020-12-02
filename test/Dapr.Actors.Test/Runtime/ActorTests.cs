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
    using System.Threading.Tasks;

    public sealed class ActorTests
    {
        [Fact]
        public void TestNewActorWithMockStateManager()
        {
            var mockStateManager = new Mock<IActorStateManager>();
            var testDemoActor = this.CreateTestDemoActor(mockStateManager.Object);
            testDemoActor.Host.Should().NotBeNull();
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

        [Theory]
        [InlineData("TestTimer", "NonExistentMethod", "Timer callback method: NonExistentMethod does not exist in the Actor class: TestActor")]
        [InlineData("TestTimer", "TimerCallbackTwoArguments", "Timer callback can accept only zero or one parameters")]
        [InlineData("TestTimer", "TimerCallbackVoid", "Timer callback can only return type Task")]
        [InlineData("TestTimer", "TimerCallbackVirtual", "Timer callback method cannot be virtual")]
        public async Task ValidateTimerCallback_CallbackMethodDoesNotMeetRequirements(string timerName, string callback, string expectedErrorMessage)
        {
            var mockStateManager = new Mock<IActorStateManager>();
            mockStateManager.Setup(manager => manager.ClearCacheAsync(It.IsAny<CancellationToken>()));
            var testDemoActor = this.CreateTestDemoActor(mockStateManager.Object);

            await FluentActions.Awaiting(async () => 
            await (testDemoActor.RegisterTimerAsync(timerName, callback, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(20))))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage(expectedErrorMessage);
        }

        /// <summary>
        /// On my test code I want to pass the mock statemanager all the time.
        /// </summary>
        /// <param name="actorStateManager">Mock StateManager.</param>
        /// <returns>TestActor.</returns>
        private TestActor CreateTestDemoActor(IActorStateManager actorStateManager)
        {
            var actorTypeInformation = ActorTypeInformation.Get(typeof(TestActor));
            var loggerFactory = new LoggerFactory();
            var host = new ActorHost(actorTypeInformation, ActorId.CreateRandom(), loggerFactory);
            var testActor = new TestActor(host, actorStateManager);
            return testActor;
        }
    }
}
