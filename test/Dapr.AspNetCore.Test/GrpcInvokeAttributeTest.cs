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
            var target = new GrpcInvokeAttribute();
            target.Should().NotBeNull();
            target.MethodName.Should().BeNull();

            target = new GrpcInvokeAttribute("mymethod");
            target.Should().NotBeNull();
            target.MethodName.Should().Be("mymethod");
        }
    }
}
