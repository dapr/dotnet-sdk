// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Client
{
    using System;
    using System.Text.Json;
    using Dapr.Actors.Test;
    using FluentAssertions;
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
        public void Create_WithIdAndActorTypeString_Succeeds()
        {
            var actorId = new ActorId("abc");
            var proxy = ActorProxy.Create(actorId, "TestActor");
            Assert.NotNull(proxy);
        }

        [Fact]
        public void Create_WithValidActorInterface_Succeeds()
        {
            var actorId = new ActorId("abc");
            var proxy = ActorProxy.Create(actorId, typeof(ITestActor), "TestActor");
            Assert.NotNull(proxy);
        }

        [Fact]
        public void Create_WithInvalidType_ThrowsArgumentException()
        {
            var actorId = new ActorId("abc");
            Action action = () => ActorProxy.Create(actorId, typeof(ActorId), "TestActor");
            action.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Create_WithCustomSerializerOnDefaultActorProxyFactory_Succeeds()
        {
            var factory = new ActorProxyFactory();
            factory.DefaultOptions.JsonSerializerOptions = new JsonSerializerOptions();

            var actorId = new ActorId("abc");
            var proxy = (ActorProxy)factory.Create(actorId, "TestActor");

            Assert.Same(factory.DefaultOptions.JsonSerializerOptions, proxy.JsonSerializerOptions);
        }

        [Fact]
        public void Create_WithCustomSerializerArgument_Succeeds()
        {
            var options = new ActorProxyOptions()
            {
                JsonSerializerOptions = new JsonSerializerOptions()
            };

            var actorId = new ActorId("abc");
            var proxy = (ActorProxy)ActorProxy.Create(actorId, typeof(ITestActor), "TestActor", options);

            Assert.Same(options.JsonSerializerOptions, proxy.JsonSerializerOptions);
        }

        [Fact]
        public void CreateGeneric_WithValidActorInterface_Succeeds()
        {
            var actorId = new ActorId("abc");
            var proxy = ActorProxy.Create<ITestActor>(actorId, "TestActor");
            Assert.NotNull(proxy);
        }

        [Fact]
        public void CreateGeneric_WithCustomSerializerOnDefaultActorProxyFactory_Succeeds()
        {
            var factory = new ActorProxyFactory();
            factory.DefaultOptions.JsonSerializerOptions = new JsonSerializerOptions();

            var actorId = new ActorId("abc");
            var proxy = (ActorProxy)factory.CreateActorProxy<ITestActor>(actorId, "TestActor");

            Assert.Same(factory.DefaultOptions.JsonSerializerOptions, proxy.JsonSerializerOptions);
        }

        [Fact]
        public void CreateGeneric_WithCustomSerializerArgument_Succeeds()
        {
            var options = new ActorProxyOptions()
            {
                JsonSerializerOptions = new JsonSerializerOptions()
            };

            var actorId = new ActorId("abc");
            var proxy = (ActorProxy)ActorProxy.Create<ITestActor>(actorId, "TestActor", options);

            Assert.Same(options.JsonSerializerOptions, proxy.JsonSerializerOptions);
        }

        [Fact]
        public void SetActorProxyFactoryDefaultOptions_ToNull_ThrowsArgumentNullException()
        {
            var factory = new ActorProxyFactory();
            Action action = () => factory.DefaultOptions = null;

            action.Should().Throw<ArgumentNullException>();
        }
    }
}
