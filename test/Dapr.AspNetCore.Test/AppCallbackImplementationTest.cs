using System;
using System.Collections.Generic;
using System.Text;
using Dapr.AspNetCore.Test.Targets;
using FluentAssertions;
using Xunit;

namespace Dapr.AspNetCore.Test
{
    public class AppCallbackImplementationTest
    {
        [Fact]
        public void ExtractMethodInfoFromGrpcBaseService_Happy()
        {
            AppCallbackImplementation.invokeMethods.Clear();
            AppCallbackImplementation.ExtractMethodInfoFromGrpcBaseService(typeof(HappyGrpcService));
            AppCallbackImplementation.invokeMethods.Count.Should().Be(2);
            AppCallbackImplementation.invokeMethods.ContainsKey("getaccount").Should().BeTrue();
            AppCallbackImplementation.invokeMethods.ContainsKey("withdraw").Should().BeTrue();
        }

        [Fact]
        public void ExtractMethodInfoFromGrpcBaseService_Unhappy_ParameterLengthCondition()
        {
            var ex = Assert.Throws<MissingMethodException>(() =>
                  AppCallbackImplementation.ExtractMethodInfoFromGrpcBaseService(typeof(UnhappyGrpcService1))
              );
            ex.Message.Should().Contain("0");
        }

        [Fact]
        public void ExtractMethodInfoFromGrpcBaseService_Unhappy_Parameter1TypeCondition()
        {
            var ex = Assert.Throws<MissingMethodException>(() =>
                  AppCallbackImplementation.ExtractMethodInfoFromGrpcBaseService(typeof(UnhappyGrpcService1))
              );
            ex.Message.Should().Contain("0");
        }
    }
}
