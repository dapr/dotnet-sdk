using System;
using System.Collections.Generic;
using System.Text;
using Dapr.AspNetCore.Test.Generated;
using Dapr.AspNetCore.Test.Targets;
using FluentAssertions;
using Xunit;

namespace Dapr.AspNetCore.Test
{
    public class GrpcInvokeAttributeTest
    {
        [Fact]
        public void Constructor_Happy()
        {
            var target = new GrpcInvokeAttribute(typeof(AccountRequest));
            target.Should().NotBeNull();
            target.InputModelType.Should().Be(typeof(AccountRequest));
            target.MethodName.Should().BeNull();

            target = new GrpcInvokeAttribute(typeof(AccountRequest), "mymethod");
            target.Should().NotBeNull();
            target.InputModelType.Should().Be(typeof(AccountRequest));
            target.MethodName.Should().Be("mymethod");
        }

        [Fact]
        public void Constructor_Unhappy()
        {
            Assert.Throws<ArgumentNullException>(() => new GrpcInvokeAttribute(null));
            Assert.Throws<ArgumentException>(() => new GrpcInvokeAttribute(typeof(AnotherAccountRequest)));
        }
    }
}
