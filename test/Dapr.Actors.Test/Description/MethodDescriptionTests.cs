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
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace Dapr.Actors.Description;

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
        description.ShouldNotBeNull();

        description.MethodInfo.ShouldBeSameAs(methodInfo);
        description.Name.ShouldBe("GetString");
        description.ReturnType.ShouldBe(typeof(Task<string>));
        description.Id.ShouldNotBe(0);
        description.Arguments.ShouldBeEmpty();
        description.HasCancellationToken.ShouldBeFalse();
    }

    [Fact]
    public void MethodDescription_CreateCrcId_WhenUseCrcIdGenerationIsSet()
    {
        // Arrange
        MethodInfo methodInfo = typeof(ITestActor).GetMethod("GetString");

        // Act
        var description = MethodDescription.Create("actor", methodInfo, true);

        // Assert
        description.Id.ShouldBe(70257263);
    }

    [Fact]
    public void MethodDescription_CreateGeneratesArgumentDescriptions_WhenMethodHasArguments()
    {
        // Arrange
        MethodInfo methodInfo = typeof(ITestActor).GetMethod("MethodWithArguments");

        // Act
        var description = MethodDescription.Create("actor", methodInfo, false);

        // Assert
        description.Arguments.ShouldNotBeNull();
        description.Arguments.ShouldBeOfType<MethodArgumentDescription[]>();
        description.Arguments.Select(m => new {m.Name}).ShouldBe(new[] {new {Name = "number"}, new {Name = "choice"}, new {Name = "information"}});
    }

    [Fact]
    public void MethodDescription_CreateSetsHasCancellationTokenToTrue_WhenMethodHasTokenAsArgument()
    {
        // Arrange
        MethodInfo methodInfo = typeof(ITestActor).GetMethod("MethodWithToken");

        // Act
        var description = MethodDescription.Create("actor", methodInfo, false);

        // Assert
        description.HasCancellationToken.ShouldBeTrue();
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
        var exception = Should.Throw<ArgumentException>(action);
        exception.Message.ShouldMatch(@"Method 'MethodWithTokenNotLast' of actor interface '.*\+ITestActor' has a '.*\.CancellationToken' parameter that is not the last parameter. If an actor method accepts a '.*\.CancellationToken' parameter, it must be the last parameter\..*");
        exception.ParamName.ShouldBe("actorInterfaceType");
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
        var exception = Should.Throw<ArgumentException>(action);
        exception.Message.ShouldMatch(@"Method 'MethodWithMultipleTokens' of actor interface '.*\+ITestActor' has a '.*\.CancellationToken' parameter that is not the last parameter. If an actor method accepts a '.*\.CancellationToken' parameter, it must be the last parameter.*");
        exception.ParamName.ShouldBe("actorInterfaceType");
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