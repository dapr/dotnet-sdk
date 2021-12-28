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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Dapr.Actors.Description
{
    public sealed class MethodDescriptionTests
    {
        [Fact]
        public void MethodDescription_CreatMethodDescription()
        {
            // Arrange
            MethodInfo methodInfo = typeof(ITestActor).GetMethod("GetString");

            // Act
            var description = MethodDescription.Create("actor", methodInfo, false);

            // Assert
            description.Should().NotBeNull();

            using var _ = new AssertionScope();
            description.MethodInfo.Should().BeSameAs(methodInfo);
            description.Name.Should().Be("GetString");
            description.ReturnType.Should().Be<Task<string>>();
            description.Id.Should().NotBe(0);
            description.Arguments.Should().BeEmpty();
            description.HasCancellationToken.Should().BeFalse();
        }

        [Fact]
        public void MethodDescription_CreateCrcId_WhenUseCrcIdGenerationIsSet()
        {
            // Arrange
            MethodInfo methodInfo = typeof(ITestActor).GetMethod("GetString");

            // Act
            var description = MethodDescription.Create("actor", methodInfo, true);

            // Assert
            description.Id.Should().Be(70257263);
        }

        [Fact]
        public void MethodDescription_CreateGeneratesArgumentDescriptions_WhenMethodHasArguments()
        {
            // Arrange
            MethodInfo methodInfo = typeof(ITestActor).GetMethod("MethodWithArguments");

            // Act
            var description = MethodDescription.Create("actor", methodInfo, false);

            // Assert
            using var _ = new AssertionScope();
            description.Arguments.Should().NotContainNulls();
            description.Arguments.Should().AllBeOfType<MethodArgumentDescription>();
            description.Arguments.Should().BeEquivalentTo(
                new { Name = "number" },
                new { Name = "choice" },
                new { Name = "information" });
        }

        [Fact]
        public void MethodDescription_CreateSetsHasCancellationTokenToTrue_WhenMethodHasTokenAsArgument()
        {
            // Arrange
            MethodInfo methodInfo = typeof(ITestActor).GetMethod("MethodWithToken");

            // Act
            var description = MethodDescription.Create("actor", methodInfo, false);

            // Assert
            description.HasCancellationToken.Should().BeTrue();
        }

        [Fact]
        public void MethodDescription_CreateThrowsArgumentException_WhenMethodHasTokenAsNonLastArgument()
        {
            // Arrange
            Type type = typeof(ITestActor);
            MethodInfo methodInfo = type.GetMethod("MethodWithTokenNotLast");

            // Act
            Action action = () => MethodDescription.Create("actor", methodInfo, false);

            // Assert
            action.Should().ThrowExactly<ArgumentException>()
                .WithMessage("Method 'MethodWithTokenNotLast' of actor interface '*+ITestActor' has a '*.CancellationToken' parameter that is not the last parameter. If an actor method accepts a '*.CancellationToken' parameter, it must be the last parameter.*")
                .And.ParamName.Should().Be("actorInterfaceType");
        }

        [Fact]
        public void MethodDescription_CreateThrowsArgumentException_WhenMethodHasMultipleCancellationTokens()
        {
            // Arrange
            Type type = typeof(ITestActor);
            MethodInfo methodInfo = type.GetMethod("MethodWithMultipleTokens");

            // Act
            Action action = () => MethodDescription.Create("actor", methodInfo, false);

            // Assert
            action.Should().ThrowExactly<ArgumentException>()
                .WithMessage("Method 'MethodWithMultipleTokens' of actor interface '*+ITestActor' has a '*.CancellationToken' parameter that is not the last parameter. If an actor method accepts a '*.CancellationToken' parameter, it must be the last parameter.*")
                .And.ParamName.Should().Be("actorInterfaceType");
        }

        internal interface ITestActor : IActor
        {
            Task<string> GetString();

            Task MethodWithArguments(int number, bool choice, string information);

            Task MethodWithToken(CancellationToken cancellationToken);

            Task MethodWithMultipleTokens(CancellationToken cancellationToken, CancellationToken cancellationTokenToo);

            Task MethodWithTokenNotLast(CancellationToken cancellationToken, bool additionalArgument);
        }
    }
}
