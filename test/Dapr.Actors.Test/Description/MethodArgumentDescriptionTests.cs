using System;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Dapr.Actors.Description
{
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
            description.Should().NotBeNull();

            using var _ = new AssertionScope();
            description.Name.Should().Be("number");
            description.ArgumentType.Should().Be<int>();
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
            action.Should().ThrowExactly<ArgumentException>()
                .WithMessage("Method 'MethodWithParams' of actor interface '*ITestActor' has variable length parameter 'values'. The actor interface methods must not have variable length parameters.*")
                .And.ParamName.Should().Be("actorInterfaceType");
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
            action.Should().ThrowExactly<ArgumentException>()
                .WithMessage("Method 'MethodWithIn' of actor interface '*ITestActor' has out/ref/optional parameter 'value'. The actor interface methods must not have out, ref or optional parameters.*")
                .And.ParamName.Should().Be("actorInterfaceType");
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
            action.Should().ThrowExactly<ArgumentException>()
                .WithMessage("Method 'MethodWithOut' of actor interface '*ITestActor' has out/ref/optional parameter 'value'. The actor interface methods must not have out, ref or optional parameters.*")
                .And.ParamName.Should().Be("actorInterfaceType");
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
            action.Should().ThrowExactly<ArgumentException>()
                .WithMessage("Method 'MethodWithOptional' of actor interface '*ITestActor' has out/ref/optional parameter 'value'. The actor interface methods must not have out, ref or optional parameters.*")
                .And.ParamName.Should().Be("actorInterfaceType");
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
}
