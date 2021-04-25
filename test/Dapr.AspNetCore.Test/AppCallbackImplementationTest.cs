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
            AppCallbackImplementation.ExtractGrpcInvoke(typeof(HappyGrpcService));
            AppCallbackImplementation.invokeMethods.Count.Should().Be(4);

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
            method.Name.Should().Be("DepositAccount");

            AppCallbackImplementation.invokeMethods.ContainsKey("close").Should().BeTrue();
            (serviceType, method) = AppCallbackImplementation.invokeMethods["close"];
            serviceType.Should().Be(typeof(HappyGrpcService));
            method.Name.Should().Be("Close");
        }

        [Fact]
        public void ExtractMethodInfoFromGrpcBaseService_Unhappy_ParameterLengthCondition()
        {
            var ex = Assert.Throws<GrpcMethodSignatureException>(() =>
                  AppCallbackImplementation.ExtractGrpcInvoke(typeof(UnhappyGrpcService0))
              );
            ex.Type.Should().Be(GrpcMethodSignatureException.ErrorType.ParameterLength);
        }

        [Fact]
        public void ExtractMethodInfoFromGrpcBaseService_Unhappy_Parameter1TypeCondition()
        {
            var ex = Assert.Throws<GrpcMethodSignatureException>(() =>
                  AppCallbackImplementation.ExtractGrpcInvoke(typeof(UnhappyGrpcService1))
              );
            ex.Type.Should().Be(GrpcMethodSignatureException.ErrorType.ParameterType);
        }

        [Fact]
        public void ExtractMethodInfoFromGrpcBaseService_Unhappy_ReturnTypeCondition()
        {
            var ex = Assert.Throws<GrpcMethodSignatureException>(() =>
                  AppCallbackImplementation.ExtractGrpcInvoke(typeof(UnhappyGrpcService2))
              );
            ex.Type.Should().Be(GrpcMethodSignatureException.ErrorType.ReturnType);

            ex = Assert.Throws<GrpcMethodSignatureException>(() =>
                  AppCallbackImplementation.ExtractGrpcInvoke(typeof(UnhappyGrpcService3))
              );
            ex.Type.Should().Be(GrpcMethodSignatureException.ErrorType.ReturnType);

            ex = Assert.Throws<GrpcMethodSignatureException>(() =>
                  AppCallbackImplementation.ExtractGrpcInvoke(typeof(UnhappyGrpcService4))
              );
            ex.Type.Should().Be(GrpcMethodSignatureException.ErrorType.ReturnGenericTypeArgument);
        }
    }
}
