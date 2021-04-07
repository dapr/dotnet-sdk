using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Dapr.AspNetCore.Test
{
    public class AppCallbackImplementationTest
    {
        [Fact]
        public void ExtractMethodInfoFromGrpcBaseService_For_HappyGrpcService()
        {
            AppCallbackImplementation.invokeMethods.Clear();
            AppCallbackImplementation.ExtractMethodInfoFromGrpcBaseService(typeof(HappyGrpcService));
            AppCallbackImplementation.invokeMethods.Count.Should().Be(2);
            AppCallbackImplementation.invokeMethods.ContainsKey("GetAccount".ToLower()).Should().BeTrue();
            AppCallbackImplementation.invokeMethods.ContainsKey("rich").Should().BeTrue();
        }
    }
}
