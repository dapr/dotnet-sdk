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
    public sealed class InterfaceDescriptionTests
    {
        [Fact]
        public void InterfaceDescription_CreateInterfaceDescription()
        {
            // Arrange
            Type type = typeof(ITestActor);

            // Act
            TestDescription description = new(type);

            // Assert
            description.Should().NotBeNull();

            using var _ = new AssertionScope();
            description.InterfaceType.Should().Be(type);
            description.Id.Should().NotBe(0);
            description.V1Id.Should().Be(0);
            description.Methods.Should().BeEmpty();
        }

        [Fact]
        public void InterfaceDescription_CreateCrcIdAndV1Id_WhenUseCrcIdGenerationIsSet()
        {
            // Arrange
            Type type = typeof(ITestActor);

            // Act
            TestDescription description = new(type, useCRCIdGeneration: true);

            // Assert
            using var _ = new AssertionScope();
            description.Id.Should().Be(-934188464);
            description.V1Id.Should().NotBe(0);
        }

        [Fact]
        public void InterfaceDescription_CreateThrowsArgumentException_WhenTypeIsGenericDefinition()
        {
            // Arrange
            Type type = typeof(IGenericActor<>);

            // Act
            Action action = () => { TestDescription _ = new(type); };

            // Assert
            action.Should().ThrowExactly<ArgumentException>()
                .WithMessage("The actor interface '*+IGenericActor`1' is using generics. Generic interfaces cannot be remoted.*")
                .And.ParamName.Should().Be("actorInterfaceType");
        }

        [Fact]
        public void InterfaceDescription_CreateThrowsArgumentException_WhenTypeIsGeneric()
        {
            // Arrange
            Type type = typeof(IGenericActor<IActor>);

            // Act
            Action action = () => { TestDescription _ = new(type); };

            // Assert
            action.Should().ThrowExactly<ArgumentException>()
                .WithMessage("The actor interface '*+IGenericActor`1[*IActor*]' is using generics. Generic interfaces cannot be remoted.*")
                .And.ParamName.Should().Be("actorInterfaceType");
        }

        [Fact]
        public void InterfaceDescription_CreateGeneratesMethodDescriptions_WhenTypeHasTaskMethods_ButDoesNotSeeInheritedMethods()
        {
            // Arrange
            Type type = typeof(IChildActor);

            // Act
            TestDescription description = new(type);

            // Assert
            using var _ = new AssertionScope();
            description.Methods.Should().NotContainNulls();
            description.Methods.Should().AllBeOfType<MethodDescription>();
            description.Methods.Should().BeEquivalentTo(
                new { Name = "GetInt" }
            );
        }

        [Fact]
        public void InterfaceDescription_CreateGeneratesMethodDescriptions_WhenTypeHasVoidMethods()
        {
            // Arrange
            Type type = typeof(IVoidActor);

            // Act
            TestDescription description = new(type, methodReturnCheck: MethodReturnCheck.EnsureReturnsVoid);

            // Assert
            using var _ = new AssertionScope();
            description.Methods.Should().NotContainNulls();
            description.Methods.Should().AllBeOfType<MethodDescription>();
            description.Methods.Should().BeEquivalentTo(
                new { Name = "GetString" },
                new { Name = "MethodWithArguments" });
        }

        [Fact]
        public void InterfaceDescription_CreateThrowsArgumentException_WhenMethodsAreNotReturningTask()
        {
            // Arrange
            Type type = typeof(IVoidActor);

            // Act
            Action action = () => { TestDescription _ = new(type); };

            // Assert
            action.Should().ThrowExactly<ArgumentException>()
                .WithMessage("Method 'GetString' of actor interface '*+IVoidActor' does not return Task or Task<>. The actor interface methods must be async and must return either Task or Task<>.*")
                .And.ParamName.Should().Be("actorInterfaceType");
        }

        [Fact]
        public void InterfaceDescription_CreateThrowsArgumentException_WhenMethodsAreNotReturningVoid()
        {
            // Arrange
            Type type = typeof(IMethodActor);

            // Act
            Action action = () => { TestDescription _ = new(type, methodReturnCheck: MethodReturnCheck.EnsureReturnsVoid); };

            // Assert
            action.Should().ThrowExactly<ArgumentException>()
                .WithMessage("Method 'GetString' of actor interface '*+IMethodActor' returns '*.Task`1[*System.String*]'*")
                .And.ParamName.Should().Be("actorInterfaceType");
        }

        [Fact]
        public void InterfaceDescription_CreateThrowsArgumentException_WhenMethodsAreOverloaded()
        {
            // Arrange
            Type type = typeof(IOverloadedMethodActor);

            // Act
            Action action = () => { TestDescription _ = new(type); };

            // Assert
            action.Should().ThrowExactly<ArgumentException>()
                .WithMessage("Method 'GetString' of actor interface '*+IOverloadedMethodActor' is overloaded. The actor interface methods cannot be overloaded.*")
                .And.ParamName.Should().Be("actorInterfaceType");
        }

        [Fact]
        public void InterfaceDescription_CreateThrowsArgumentException_WhenMethodIsGeneric()
        {
            // Arrange
            Type type = typeof(IGenericMethodActor);

            // Act
            Action action = () => { TestDescription _ = new(type); };

            // Assert
            action.Should().ThrowExactly<ArgumentException>()
                .WithMessage("Method 'Get' of actor interface '*+IGenericMethodActor' is using generics. The actor interface methods cannot use generics.*")
                .And.ParamName.Should().Be("actorInterfaceType");
        }

        [Fact]
        public void InterfaceDescription_CreateThrowsArgumentException_WhenMethodHasVariableArguments()
        {
            // Arrange
            Type type = typeof(IVariableActor);

            // Act
            Action action = () => { TestDescription _ = new(type); };

            // Assert
            action.Should().ThrowExactly<ArgumentException>()
                .WithMessage("Method 'MethodWithVarArgs' of actor interface '*+IVariableActor' is using a variable argument list. The actor interface methods cannot have a variable argument list.*")
                .And.ParamName.Should().Be("actorInterfaceType");
        }

        internal interface ITestActor : IActor
        {
        }

        internal interface IGenericActor<T> : IActor
        {
        }

        internal interface IMethodActor : IActor
        {
            Task<string> GetString();

            Task MethodWithArguments(int number, bool choice, string information);
        }

        internal interface IChildActor : IMethodActor
        {
            Task<int> GetInt();
        }

        internal interface IVariableActor : IActor
        {
            Task MethodWithVarArgs(__arglist);
        }

        internal interface IVoidActor : IActor
        {
            void GetString();

            void MethodWithArguments(int number, bool choice, string information);
        }

        internal interface IOverloadedActor : IMethodActor
        {
            Task<string> GetString(string parameter);
        }

        internal interface IOverloadedMethodActor : IActor
        {
            Task<string> GetString();

            Task<string> GetString(string parameter);
        }

        internal interface IGenericMethodActor : IActor
        {
            Task<T> Get<T>();
        }

        internal class TestDescription : InterfaceDescription
        {
            public TestDescription(
                Type remotedInterfaceType,
                string remotedInterfaceKindName = "actor", 
                bool useCRCIdGeneration = false, 
                MethodReturnCheck methodReturnCheck = MethodReturnCheck.EnsureReturnsTask)
                : base(remotedInterfaceKindName, remotedInterfaceType, useCRCIdGeneration, methodReturnCheck)
            {
            }
        }
    }
}
