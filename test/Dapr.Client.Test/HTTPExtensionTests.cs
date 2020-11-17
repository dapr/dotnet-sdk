using System;
using System.Collections.Generic;
using System.Text;
using Dapr.Client.Http;
using FluentAssertions;
using Xunit;

namespace Dapr.Client.Test
{
    public class HTTPExtensionTests
    {
        [Fact]
        public void HTTPExtension_HttpVerb_FluentMethods_CreateInstanceWithRightMethod()
        {
            foreach (var key in Enum.GetValues(typeof(HTTPVerb)))
            {
                HTTPExtension result = null;
                switch ((HTTPVerb)key)
                {
                    case HTTPVerb.Get:
                        result = HTTPExtension.UsingGet();
                        break;
                    case HTTPVerb.Head:
                        result = HTTPExtension.UsingHead();
                        break;
                    case HTTPVerb.Post:
                        result = HTTPExtension.UsingPost();
                        break;
                    case HTTPVerb.Put:
                        result = HTTPExtension.UsingPut();
                        break;
                    case HTTPVerb.Delete:
                        result = HTTPExtension.UsingDelete();
                        break;
                    case HTTPVerb.Connect:
                        result = HTTPExtension.UsingConnect();
                        break;
                    case HTTPVerb.Options:
                        result = HTTPExtension.UsingOptions();
                        break;
                    case HTTPVerb.Trace:
                        result = HTTPExtension.UsingTrace();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                result.Verb.Should().BeEquivalentTo((HTTPVerb)key);
            }
        }

        [Fact]
        public void HTTPExtension_QueryString_FluentMethods_CreateInstanceWithRightPayload()
        {
            var request = HTTPExtension
                .UsingPost()
                .WithQueryParam("key1", "value1")
                .WithQueryParam("key2", "value2")
                .WithQueryParam(new KeyValuePair<string, string>("key3", "value3"));

            request.QueryString["key1"].Should().Be("value1");
            request.QueryString["key2"].Should().Be("value2");
            request.QueryString["key3"].Should().Be("value3");
        }

        [Fact]
        public void HTTPExtension_Headers_FluentMethods_CreateInstanceWithRightPayload()
        {
            var request = HTTPExtension
                .UsingPost()
                .WithHeader("key1", "value1")
                .WithHeader("key2", "value2")
                .WithHeader(new KeyValuePair<string, string>("key3", "value3"));

            request.Headers["key1"].Should().Be("value1");
            request.Headers["key2"].Should().Be("value2");
            request.Headers["key3"].Should().Be("value3");
        }
    }
}
