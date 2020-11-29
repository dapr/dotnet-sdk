using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using Xunit;
using GrpcVerb = Dapr.Client.Autogen.Grpc.v1.HTTPExtension.Types.Verb;

namespace Dapr.Client.Test
{
    public class HttpInvocationOptionsTests
    {
        [Fact]
        public void HttpInvocationOptions_HttpMethod_FluentMethods_CreateInstanceWithRightMethod()
        {
            // We have a convenience method for each of the enum values in the .proto except "None"
            foreach (var key in Enum.GetValues(typeof(GrpcVerb)).Cast<GrpcVerb>())
            {
                if (key == GrpcVerb.None)
                {
                    continue;
                }

                var result = key switch
                {
                    GrpcVerb.Get => HttpInvocationOptions.UsingGet(),
                    GrpcVerb.Head => HttpInvocationOptions.UsingHead(),
                    GrpcVerb.Post => HttpInvocationOptions.UsingPost(),
                    GrpcVerb.Put => HttpInvocationOptions.UsingPut(),
                    GrpcVerb.Delete => HttpInvocationOptions.UsingDelete(),
                    GrpcVerb.Connect => HttpInvocationOptions.UsingConnect(),
                    GrpcVerb.Options => HttpInvocationOptions.UsingOptions(),
                    GrpcVerb.Trace => HttpInvocationOptions.UsingTrace(),
                    _ => throw new ArgumentOutOfRangeException(),
                };
                
                result.Method.Method.ToUpperInvariant().Should().BeEquivalentTo(key.ToString().ToUpperInvariant());
            }
        }

        [Fact]
        public void HttpInvocationOptions_QueryString_FluentMethods_CreateInstanceWithRightPayload()
        {
            var options = HttpInvocationOptions
                .UsingPost()
                .WithQueryParam("key1", "value1")
                .WithQueryParam("key2", "value2")
                .WithQueryParam(new KeyValuePair<string, string>("key3", "value3"));

            options.QueryString["key1"].Should().Be("value1");
            options.QueryString["key2"].Should().Be("value2");
            options.QueryString["key3"].Should().Be("value3");
        }

        [Fact]
        public void HttpInvocationOptions_Headers_FluentMethods_CreateInstanceWithRightPayload()
        {
            var options = HttpInvocationOptions
                .UsingPost()
                .WithHeader("key1", "value1")
                .WithHeader("key2", "value2")
                .WithHeader(new KeyValuePair<string, string>("key3", "value3"));

            options.Headers["key1"].Should().Be("value1");
            options.Headers["key2"].Should().Be("value2");
            options.Headers["key3"].Should().Be("value3");
        }
    }
}
