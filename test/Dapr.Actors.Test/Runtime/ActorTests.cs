﻿// ------------------------------------------------------------
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
    using Dapr.Actors.Client;

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
        public async Task TestSaveState()
        {
            var mockStateManager = new Mock<IActorStateManager>();
            mockStateManager.Setup(manager => manager.SaveStateAsync(It.IsAny<CancellationToken>()));
            var testDemoActor = this.CreateTestDemoActor(mockStateManager.Object);
            await testDemoActor.SaveTestState();
            mockStateManager.Verify(manager => manager.SaveStateAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task TestResetStateAsync()
        {
            var mockStateManager = new Mock<IActorStateManager>();
            mockStateManager.Setup(manager => manager.ClearCacheAsync(It.IsAny<CancellationToken>()));
            var testDemoActor = this.CreateTestDemoActor(mockStateManager.Object);
            await testDemoActor.ResetTestStateAsync();
            mockStateManager.Verify(manager => manager.ClearCacheAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData("NonExistentMethod", "Timer callback method: NonExistentMethod does not exist in the Actor class: TestActor")]
        [InlineData("TimerCallbackTwoArguments", "Timer callback can accept only zero or one parameters")]
        [InlineData("TimerCallbackNonTaskReturnType", "Timer callback can only return type Task")]
        [InlineData("TimerCallbackOverloaded", "Timer callback method: TimerCallbackOverloaded cannot be overloaded.")]
        public void ValidateTimerCallback_CallbackMethodDoesNotMeetRequirements(string callback, string expectedErrorMessage)
        {
            var mockStateManager = new Mock<IActorStateManager>();
            mockStateManager.Setup(manager => manager.ClearCacheAsync(It.IsAny<CancellationToken>()));
            var testDemoActor = this.CreateTestDemoActor(mockStateManager.Object);

            ;
            FluentActions.Invoking(() =>
                testDemoActor.ValidateTimerCallback(testDemoActor.Host, callback))
            .Should().Throw<ArgumentException>()
            .WithMessage(expectedErrorMessage);
        }

        [Theory]
        [InlineData("TimerCallbackPrivate")]
        [InlineData("TimerCallbackProtected")]
        [InlineData("TimerCallbackInternal")]
        [InlineData("TimerCallbackPublicWithNoArguments")]
        [InlineData("TimerCallbackPublicWithOneArgument")]
        [InlineData("TimerCallbackStatic")]
        public void ValidateTimerCallback_CallbackMethodMeetsRequirements(string callback)
        {
            var mockStateManager = new Mock<IActorStateManager>();
            mockStateManager.Setup(manager => manager.ClearCacheAsync(It.IsAny<CancellationToken>()));
            var testDemoActor = this.CreateTestDemoActor(mockStateManager.Object);

            ;
            FluentActions.Invoking(() =>
                testDemoActor.ValidateTimerCallback(testDemoActor.Host, callback))
            .Should().NotThrow();
        }

        [Theory]
        [InlineData("TimerCallbackPrivate")]
        [InlineData("TimerCallbackPublicWithOneArgument")]
        [InlineData("TimerCallbackStatic")]
        public void GetMethodInfoUsingReflection_MethodsMatchingBindingFlags(string callback)
        {
            var mockStateManager = new Mock<IActorStateManager>();
            mockStateManager.Setup(manager => manager.ClearCacheAsync(It.IsAny<CancellationToken>()));
            var testDemoActor = this.CreateTestDemoActor(mockStateManager.Object);
            var methodInfo = testDemoActor.GetMethodInfoUsingReflection(testDemoActor.Host.ActorTypeInfo.ImplementationType, callback);
            Assert.NotNull(methodInfo);
        }

        [Theory]
        [InlineData("TestActor")] // Constructor
        public void GetMethodInfoUsingReflection_MethodsNotMatchingBindingFlags(string callback)
        {
            var mockStateManager = new Mock<IActorStateManager>();
            mockStateManager.Setup(manager => manager.ClearCacheAsync(It.IsAny<CancellationToken>()));
            var testDemoActor = this.CreateTestDemoActor(mockStateManager.Object);
            var methodInfo = testDemoActor.GetMethodInfoUsingReflection(testDemoActor.Host.ActorTypeInfo.ImplementationType, callback);
            Assert.Null(methodInfo);
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
            var host = new ActorHost(actorTypeInformation, ActorId.CreateRandom(), JsonSerializerDefaults.Web, loggerFactory, ActorProxy.DefaultProxyFactory);
            var testActor = new TestActor(host, actorStateManager);
            return testActor;
        }

    }
}
