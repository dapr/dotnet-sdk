using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
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
            AppCallbackImplementation.invokeMethods.Count.Should().Be(3);

            AppCallbackImplementation.invokeMethods.ContainsKey("getaccount").Should().BeTrue();
            var (serviceType, method) = AppCallbackImplementation.invokeMethods["getaccount"];
            serviceType.Should().Be(typeof(HappyGrpcService));
            method.Name.Should().Be("GetAccount");

            AppCallbackImplementation.invokeMethods.ContainsKey("withdraw").Should().BeTrue();
            (serviceType, method) = AppCallbackImplementation.invokeMethods["withdraw"];
            serviceType.Should().Be(typeof(HappyGrpcService));
            method.Name.Should().Be("WithdrawAccount");

            AppCallbackImplementation.invokeMethods.ContainsKey("deposit").Should().BeTrue();
            (serviceType, method) = AppCallbackImplementation.invokeMethods["deposit"];
            serviceType.Should().Be(typeof(HappyGrpcService));
            method.Name.Should().Be("Deposit");
        }

        [Fact]
        public void ExtractMethodInfoFromGrpcBaseService_Unhappy_ParameterLengthCondition()
        {
            var ex = Assert.Throws<MissingMethodException>(() =>
                  AppCallbackImplementation.ExtractMethodInfoFromGrpcBaseService(typeof(UnhappyGrpcService0))
              );
            ex.Message.Should().Contain("0");
        }

        [Fact]
        public void ExtractMethodInfoFromGrpcBaseService_Unhappy_Parameter1TypeCondition()
        {
            var ex = Assert.Throws<MissingMethodException>(() =>
                  AppCallbackImplementation.ExtractMethodInfoFromGrpcBaseService(typeof(UnhappyGrpcService1))
              );
            ex.Message.Should().Contain("1");
        }

        [Fact]
        public void ExtractMethodInfoFromGrpcBaseService_Unhappy_Parameter2TypeCondition()
        {
            var ex = Assert.Throws<MissingMethodException>(() =>
                  AppCallbackImplementation.ExtractMethodInfoFromGrpcBaseService(typeof(UnhappyGrpcService2))
              );
            ex.Message.Should().Contain("2");
        }

        [Fact]
        public void ExtractMethodInfoFromGrpcBaseService_Unhappy_ReturnTypeCondition()
        {
            var ex = Assert.Throws<MissingMethodException>(() =>
                  AppCallbackImplementation.ExtractMethodInfoFromGrpcBaseService(typeof(UnhappyGrpcService3a))
              );
            ex.Message.Should().Contain("3");

            ex = Assert.Throws<MissingMethodException>(() =>
                  AppCallbackImplementation.ExtractMethodInfoFromGrpcBaseService(typeof(UnhappyGrpcService3b))
              );
            ex.Message.Should().Contain("3");

            ex = Assert.Throws<MissingMethodException>(() =>
                  AppCallbackImplementation.ExtractMethodInfoFromGrpcBaseService(typeof(UnhappyGrpcService4))
              );
            ex.Message.Should().Contain("4");
        }
    }
}
