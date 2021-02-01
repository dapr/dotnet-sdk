﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Test.Runtime
{
    using Dapr.Actors.Runtime;
    using Moq;
    using Xunit;
    using Microsoft.Extensions.Logging;
    using System;
    using FluentAssertions;
    using Dapr.Actors.Client;

    public sealed class ActorRuntimeOptionsTests
    {
        [Fact]
        public void TestRegisterActor_SavesActivator()
        {
            var actorType = typeof(TestActor);
            var actorTypeInformation = ActorTypeInformation.Get(actorType);
            var host = new ActorHost(actorTypeInformation, ActorId.CreateRandom(), JsonSerializerDefaults.Web, new LoggerFactory(), ActorProxy.DefaultProxyFactory);
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

        [Fact]
        public void SettingActorIdleTimeout_Succeeds()
        {
            var options = new ActorRuntimeOptions();
            options.ActorIdleTimeout = TimeSpan.FromSeconds(1);

            Assert.Equal(TimeSpan.FromSeconds(1), options.ActorIdleTimeout);
        }

        [Fact]
        public void SettingActorIdleTimeoutToLessThanZero_Fails()
        {
            var options = new ActorRuntimeOptions();
            Action action = () => options.ActorIdleTimeout = TimeSpan.FromSeconds(-1);

            action.Should().Throw<ArgumentOutOfRangeException>();
        }


        [Fact]
        public void SettingActorScanInterval_Succeeds()
        {
            var options = new ActorRuntimeOptions();
            options.ActorScanInterval = TimeSpan.FromSeconds(1);

            Assert.Equal(TimeSpan.FromSeconds(1), options.ActorScanInterval);
        }

        [Fact]
        public void SettingActorScanIntervalToLessThanZero_Fails()
        {
            var options = new ActorRuntimeOptions();
            Action action = () => options.ActorScanInterval = TimeSpan.FromSeconds(-1);

            action.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void SettingDrainOngoingCallTimeout_Succeeds()
        {
            var options = new ActorRuntimeOptions();
            options.DrainOngoingCallTimeout = TimeSpan.FromSeconds(1);

            Assert.Equal(TimeSpan.FromSeconds(1), options.DrainOngoingCallTimeout);
        }

        [Fact]
        public void SettingDrainOngoingCallTimeoutToLessThanZero_Fails()
        {
            var options = new ActorRuntimeOptions();
            Action action = () => options.DrainOngoingCallTimeout = TimeSpan.FromSeconds(-1);

            action.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void SettingJsonSerializerOptions_Succeeds()
        {
            var serializerOptions = new System.Text.Json.JsonSerializerOptions();
            var options = new ActorRuntimeOptions();
            options.JsonSerializerOptions = serializerOptions;

            Assert.Same(serializerOptions, options.JsonSerializerOptions);
        }

        [Fact]
        public void SettingJsonSerializerOptionsToNull_Fails()
        {
            var options = new ActorRuntimeOptions();
            Action action = () => options.JsonSerializerOptions = null;

            action.Should().Throw<ArgumentNullException>();
        }
    }
}
