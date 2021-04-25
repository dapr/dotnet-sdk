﻿using System;
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
        public void ExtractGrpcInvoke_Happy()
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
        public void ExtractGrpcInvoke_Unhappy_ParameterCondition()
        {
            var ex = Assert.Throws<GrpcMethodSignatureException>(() =>
                  AppCallbackImplementation.ExtractGrpcInvoke(typeof(UnhappyGrpcService0))
              );
            ex.Type.Should().Be(GrpcMethodSignatureException.ErrorType.ParameterLength);

            ex = Assert.Throws<GrpcMethodSignatureException>(() =>
                  AppCallbackImplementation.ExtractGrpcInvoke(typeof(UnhappyGrpcService1))
              );
            ex.Type.Should().Be(GrpcMethodSignatureException.ErrorType.ParameterType);
        }

        [Fact]
        public void ExtractGrpcInvoke_Unhappy_ReturnTypeCondition()
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

        [Fact]
        public void ExtractTopic_Happy()
        {
            AppCallbackImplementation.topicMethods.Clear();
            AppCallbackImplementation.ExtractTopic(typeof(HappyGrpcService));
            AppCallbackImplementation.topicMethods.Count.Should().Be(2);

            AppCallbackImplementation.topicMethods.ContainsKey("pubsub|deposit").Should().BeTrue();
            var (serviceType, method) = AppCallbackImplementation.topicMethods["pubsub|deposit"];
            serviceType.Should().Be(typeof(HappyGrpcService));
            method.Name.Should().Be("DepositAccount");

            AppCallbackImplementation.topicMethods.ContainsKey("pubsub|withdraw").Should().BeTrue();
            (serviceType, method) = AppCallbackImplementation.topicMethods["pubsub|withdraw"];
            serviceType.Should().Be(typeof(HappyGrpcService));
            method.Name.Should().Be("WithdrawAccount");
        }
    }
}
