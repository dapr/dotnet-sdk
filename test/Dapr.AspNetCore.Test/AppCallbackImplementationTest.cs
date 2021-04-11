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
            //throw new MissingMethodException("Service Invocation method must have two parameters. ErrorNumber: 0");
            var ex = Assert.Throws<MissingMethodException>(() =>
                  AppCallbackImplementation.ExtractMethodInfoFromGrpcBaseService(typeof(UnhappyGrpcService0))
              );
            ex.Message.Should().Contain("0");
        }

        [Fact]
        public void ExtractMethodInfoFromGrpcBaseService_Unhappy_Parameter1TypeCondition()
        {
            //throw new MissingMethodException("The type of first parameter must derive from Google.Protobuf.IMessage. ErrorNumber: 1");
            var ex = Assert.Throws<MissingMethodException>(() =>
                  AppCallbackImplementation.ExtractMethodInfoFromGrpcBaseService(typeof(UnhappyGrpcService1))
              );
            ex.Message.Should().Contain("1");

            //throw new MissingMethodException("The type of first parameter must equals with InputModelType. ErrorNumber: 2");
            ex = Assert.Throws<MissingMethodException>(() =>
                 AppCallbackImplementation.ExtractMethodInfoFromGrpcBaseService(typeof(UnhappyGrpcService2))
             );
            ex.Message.Should().Contain("2");
        }

        [Fact]
        public void ExtractMethodInfoFromGrpcBaseService_Unhappy_Parameter2TypeCondition()
        {
            //throw new MissingMethodException("The type of second parameter must be Grpc.CoreServerCallContext. ErrorNumber: 3");
            var ex = Assert.Throws<MissingMethodException>(() =>
                  AppCallbackImplementation.ExtractMethodInfoFromGrpcBaseService(typeof(UnhappyGrpcService3))
              );
            ex.Message.Should().Contain("3");
        }

        [Fact]
        public void ExtractMethodInfoFromGrpcBaseService_Unhappy_ReturnTypeCondition()
        {
            //throw new MissingMethodException("The return type must be Task<>. ErrorNumber: 4");
            var ex = Assert.Throws<MissingMethodException>(() =>
                  AppCallbackImplementation.ExtractMethodInfoFromGrpcBaseService(typeof(UnhappyGrpcService4))
              );
            ex.Message.Should().Contain("4");

            //throw new MissingMethodException("The type of return type's generic type must derive from Google.Protobuf.IMessage. ErrorNumber: 5");
            ex = Assert.Throws<MissingMethodException>(() =>
                  AppCallbackImplementation.ExtractMethodInfoFromGrpcBaseService(typeof(UnhappyGrpcService5))
              );
            ex.Message.Should().Contain("5");
        }
    }
}
