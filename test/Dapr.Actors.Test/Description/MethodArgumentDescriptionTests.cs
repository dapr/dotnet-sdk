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

using System;
using System.Reflection;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace Dapr.Actors.Description;

public sealed class MethodArgumentDescriptionTests
{
    [Fact]
    public void MethodArgumentDescription_CreatMethodArgumentDescription()
    {
        // Arrange
        MethodInfo methodInfo = typeof(ITestActor).GetMethod("MethodWithArguments");
        ParameterInfo parameterInfo = methodInfo.GetParameters()[0];

        // Act
        var description = MethodArgumentDescription.Create("actor", methodInfo, parameterInfo);

        // Assert
        description.ShouldNotBeNull();

        description.Name.ShouldBe("number");
        description.ArgumentType.ShouldBe(typeof(int));
    }

    [Fact]
    public void MethodDescription_CreateThrowsArgumentException_WhenParameterHasVariableLength()
    {
        // Arrange
        Type type = typeof(ITestActor);
        MethodInfo methodInfo = type.GetMethod("MethodWithParams");
        ParameterInfo parameterInfo = methodInfo.GetParameters()[0];

        // Act
        Action action = () => MethodArgumentDescription.Create("actor", methodInfo, parameterInfo);

        // Assert
        var exception = Should.Throw<ArgumentException>(action);
        exception.Message.ShouldMatch(@"Method 'MethodWithParams' of actor interface '.*\+ITestActor' has variable length parameter 'values'. The actor interface methods must not have variable length parameters.*");
        exception.ParamName.ShouldBe("actorInterfaceType");
    }

    [Fact]
    public void MethodDescription_CreateThrowsArgumentException_WhenParameterIsInput()
    {
        // Arrange
        Type type = typeof(ITestActor);
        MethodInfo methodInfo = type.GetMethod("MethodWithIn");
        ParameterInfo parameterInfo = methodInfo.GetParameters()[0];

        // Act
        Action action = () => MethodArgumentDescription.Create("actor", methodInfo, parameterInfo);

        // Assert
        var exception = Should.Throw<ArgumentException>(action);
        exception.Message.ShouldMatch(@"Method 'MethodWithIn' of actor interface '.*\+ITestActor' has out/ref/optional parameter 'value'. The actor interface methods must not have out, ref or optional parameters.*");
        exception.ParamName.ShouldBe("actorInterfaceType");
    }

    [Fact]
    public void MethodDescription_CreateThrowsArgumentException_WhenParameterIsOutput()
    {
        // Arrange
        Type type = typeof(ITestActor);
        MethodInfo methodInfo = type.GetMethod("MethodWithOut");
        ParameterInfo parameterInfo = methodInfo.GetParameters()[0];

        // Act
        Action action = () => MethodArgumentDescription.Create("actor", methodInfo, parameterInfo);

        // Assert
        var exception = Should.Throw<ArgumentException>(action);
        exception.Message.ShouldMatch(@"Method 'MethodWithOut' of actor interface '.*\+ITestActor' has out/ref/optional parameter 'value'. The actor interface methods must not have out, ref or optional parameters.*");
        exception.ParamName.ShouldBe("actorInterfaceType");
    }

    [Fact]
    public void MethodDescription_CreateThrowsArgumentException_WhenParameterIsOptional()
    {
        // Arrange
        Type type = typeof(ITestActor);
        MethodInfo methodInfo = type.GetMethod("MethodWithOptional");
        ParameterInfo parameterInfo = methodInfo.GetParameters()[0];

        // Act
        Action action = () => MethodArgumentDescription.Create("actor", methodInfo, parameterInfo);

        // Assert
        var exception = Should.Throw<ArgumentException>(action);
        exception.Message.ShouldMatch(@"Method 'MethodWithOptional' of actor interface '.*\+ITestActor' has out/ref/optional parameter 'value'. The actor interface methods must not have out, ref or optional parameters.*");
        exception.ParamName.ShouldBe("actorInterfaceType");
    }

    internal interface ITestActor : IActor
    {
        Task MethodWithArguments(int number, bool choice, string information);

        Task MethodWithParams(params string[] values);

        Task MethodWithOut(out int value);

        Task MethodWithIn(in int value);

        Task MethodWithOptional(int value = 1);
    }
}