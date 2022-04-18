// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

﻿using System;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Dapr.Actors.Description
{
    public sealed class ActorInterfaceDescriptionTests
    {
        [Fact]
        public void ActorInterfaceDescription_CreateActorInterfaceDescription()
        {
            // Arrange
            Type type = typeof(ITestActor);
            
            // Act
            var description = ActorInterfaceDescription.Create(type);

            // Assert
            description.Should().NotBeNull();
            
            using var _ = new AssertionScope();
            description.InterfaceType.Should().Be(type);
            description.Id.Should().NotBe(0);
            description.V1Id.Should().Be(0);
            description.Methods.Should().HaveCount(2);
        }

        [Fact]
        public void ActorInterfaceDescription_CreateThrowsArgumentException_WhenTypeIsNotAnInterface()
        {
            // Arrange
            Type type = typeof(object);

            // Act
            Action action = () => ActorInterfaceDescription.Create(type);

            // Assert
            action.Should().ThrowExactly<ArgumentException>()
                .WithMessage("The type 'System.Object' is not an Actor interface as it is not an interface.*")
                .And.ParamName.Should().Be("actorInterfaceType");

        }

        [Fact]
        public void ActorInterfaceDescription_CreateThrowsArgumentException_WhenTypeIsNotAnActorInterface()
        {
            // Arrange
            Type type = typeof(ICloneable);

            // Act
            Action action = () => ActorInterfaceDescription.Create(type);

            // Assert
            action.Should().ThrowExactly<ArgumentException>()
                .WithMessage("The type 'System.ICloneable' is not an actor interface as it does not derive from the interface 'Dapr.Actors.IActor'.*")
                .And.ParamName.Should().Be("actorInterfaceType");
        }

        [Fact]
        public void ActorInterfaceDescription_CreateThrowsArgumentException_WhenActorInterfaceInheritsNonActorInterfaces()
        {
            // Arrange
            Type type = typeof(IClonableActor);

            // Act
            Action action = () => ActorInterfaceDescription.Create(type);

            // Assert
            action.Should().ThrowExactly<ArgumentException>()
                .WithMessage("The type '*+IClonableActor' is not an actor interface as it derive from a non actor interface 'System.ICloneable'. All actor interfaces must derive from 'Dapr.Actors.IActor'.*")
                .And.ParamName.Should().Be("actorInterfaceType");
        }

        [Fact]
        public void ActorInterfaceDescription_CreateUsingCRCIdActorInterfaceDescription()
        {
            // Arrange
            Type type = typeof(ITestActor);

            // Act
            var description = ActorInterfaceDescription.CreateUsingCRCId(type);

            // Assert
            description.Should().NotBeNull();

            using var _ = new AssertionScope();
            description.InterfaceType.Should().Be(type);
            description.Id.Should().Be(-934188464);
            description.V1Id.Should().NotBe(0);
            description.Methods.Should().HaveCount(2);
        }

        [Fact]
        public void ActorInterfaceDescription_CreateUsingCRCIdThrowsArgumentException_WhenTypeIsNotAnInterface()
        {
            // Arrange
            Type type = typeof(object);

            // Act
            Action action = () => ActorInterfaceDescription.CreateUsingCRCId(type);

            // Assert
            action.Should().ThrowExactly<ArgumentException>()
                .WithMessage("The type 'System.Object' is not an Actor interface as it is not an interface.*")
                .And.ParamName.Should().Be("actorInterfaceType");
        }

        [Fact]
        public void ActorInterfaceDescription_CreateUsingCRCIdThrowsArgumentException_WhenTypeIsNotAnActorInterface()
        {
            // Arrange
            Type type = typeof(ICloneable);

            // Act
            Action action = () => ActorInterfaceDescription.CreateUsingCRCId(type);

            // Assert
            action.Should().ThrowExactly<ArgumentException>()
                .WithMessage("The type 'System.ICloneable' is not an actor interface as it does not derive from the interface 'Dapr.Actors.IActor'.*")
                .And.ParamName.Should().Be("actorInterfaceType");
        }

        [Fact]
        public void ActorInterfaceDescription_CreateUsingCRCIdThrowsArgumentException_WhenActorInterfaceInheritsNonActorInterfaces()
        {
            // Arrange
            Type type = typeof(IClonableActor);

            // Act
            Action action = () => ActorInterfaceDescription.CreateUsingCRCId(type);

            // Assert
            action.Should().ThrowExactly<ArgumentException>()
                .WithMessage("The type '*+IClonableActor' is not an actor interface as it derive from a non actor interface 'System.ICloneable'. All actor interfaces must derive from 'Dapr.Actors.IActor'.*")
                .And.ParamName.Should().Be("actorInterfaceType");
        }

        internal interface IClonableActor : ICloneable, IActor
        {
        }

        internal interface ITestActor : IActor
        {
            Task<string> GetString();

            Task MethodWithArguments(int number, bool choice, string information);
        }
    }
}
