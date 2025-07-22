// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

namespace Dapr.AspNetCore.Test;

using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Xunit;

public class CloudEventsMiddlewareTest
{
    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/json")] // "binary" format
    [InlineData("application/cloudevents")] // no format
    [InlineData("application/cloudevents+xml")] // wrong format
    [InlineData("application/cloudevents-batch+json")] // we don't support batch
    public async Task InvokeAsync_IgnoresOtherContentTypes(string contentType)
    {
        var serviceCollection = new ServiceCollection();
        var provider = serviceCollection.BuildServiceProvider();
            
        var app = new ApplicationBuilder(provider);
        app.UseCloudEvents();

        // Do verification in the scope of the middleware
        app.Run(httpContext =>
        {
            httpContext.Request.ContentType.ShouldBe(contentType);
            ReadBody(httpContext.Request.Body).ShouldBe("Hello, world!");
            return Task.CompletedTask;
        });

        var pipeline = app.Build();

        var context = new DefaultHttpContext
        {
            Request = { ContentType = contentType, Body = MakeBody("Hello, world!") }
        };

        await pipeline.Invoke(context);
    }

    [Theory]
    [InlineData(null, null)] // assumes application/json + utf8
    [InlineData("application/json", null)] // assumes utf8
    [InlineData("application/json", "utf-8")]
    [InlineData("application/json", "UTF-8")]
    [InlineData("application/person+json", "UTF-16")] // arbitrary content type and charset
    public async Task InvokeAsync_ReplacesBodyJson(string dataContentType, string charSet)
    {
        var encoding = charSet == null ? null : Encoding.GetEncoding(charSet);
        var serviceCollection = new ServiceCollection();
        var provider = serviceCollection.BuildServiceProvider();
            
        var app = new ApplicationBuilder(provider);
        app.UseCloudEvents();

        // Do verification in the scope of the middleware
        app.Run(httpContext =>
        {
            httpContext.Request.ContentType.ShouldBe(dataContentType ?? "application/json");
            ReadBody(httpContext.Request.Body).ShouldBe("{\"name\":\"jimmy\"}");
            return Task.CompletedTask;
        });

        var pipeline = app.Build();

        var context = new DefaultHttpContext { Request =
            {
                ContentType =
                    charSet == null
                        ? "application/cloudevents+json"
                        : $"application/cloudevents+json;charset={charSet}",
                Body = dataContentType == null ?
                    MakeBody("{ \"data\": { \"name\":\"jimmy\" } }", encoding) :
                    MakeBody($"{{ \"datacontenttype\": \"{dataContentType}\", \"data\": {{ \"name\":\"jimmy\" }} }}", encoding)
            }
        };

        await pipeline.Invoke(context);
    }
        
    [Theory]
    [InlineData(null, null)] // assumes application/json + utf8
    [InlineData("application/json", null)] // assumes utf8
    [InlineData("application/json", "utf-8")]
    [InlineData("application/json", "UTF-8")]
    [InlineData("application/person+json", "UTF-16")] // arbitrary content type and charset
    public async Task InvokeAsync_ReplacesPascalCasedBodyJson(string dataContentType, string charSet)
    {
        var encoding = charSet == null ? null : Encoding.GetEncoding(charSet);
        var serviceCollection = new ServiceCollection();
        var provider = serviceCollection.BuildServiceProvider();
            
        var app = new ApplicationBuilder(provider);
        app.UseCloudEvents();

        // Do verification in the scope of the middleware
        app.Run(httpContext =>
        {
            httpContext.Request.ContentType.ShouldBe(dataContentType ?? "application/json");
            ReadBody(httpContext.Request.Body).ShouldBe("{\"name\":\"jimmy\"}");
            return Task.CompletedTask;
        });

        var pipeline = app.Build();

        var context = new DefaultHttpContext { Request =
            {
                ContentType =
                    charSet == null
                        ? "application/cloudevents+json"
                        : $"application/cloudevents+json;charset={charSet}",
                Body = dataContentType == null ?
                    MakeBody("{ \"Data\": { \"name\":\"jimmy\" } }", encoding) :
                    MakeBody($"{{ \"DataContentType\": \"{dataContentType}\", \"Data\": {{ \"name\":\"jimmy\" }} }}", encoding)
            }
        };

        await pipeline.Invoke(context);
    }
        
    [Theory]
    [InlineData(null, null)] // assumes application/json + utf8
    [InlineData("application/json", null)] // assumes utf8
    [InlineData("application/json", "utf-8")]
    [InlineData("application/json", "UTF-8")]
    [InlineData("application/person+json", "UTF-16")] // arbitrary content type and charset
    public async Task InvokeAsync_ForwardsJsonPropertiesAsHeaders(string dataContentType, string charSet)
    {
        var encoding = charSet == null ? null : Encoding.GetEncoding(charSet);
        var serviceCollection = new ServiceCollection();
        var provider = serviceCollection.BuildServiceProvider();
            
        var app = new ApplicationBuilder(provider);
        app.UseCloudEvents(new CloudEventsMiddlewareOptions
        {
            ForwardCloudEventPropertiesAsHeaders = true
        });

        // Do verification in the scope of the middleware
        app.Run(httpContext =>
        {
            httpContext.Request.ContentType.ShouldBe(dataContentType ?? "application/json");
            ReadBody(httpContext.Request.Body).ShouldBe("{\"name\":\"jimmy\"}");

            httpContext.Request.Headers.ShouldContainKey("Cloudevent.type");
            httpContext.Request.Headers["Cloudevent.type"].ToString().ShouldBe("Test.Type");
            httpContext.Request.Headers.ShouldContainKey("Cloudevent.subject");
            httpContext.Request.Headers["Cloudevent.subject"].ToString().ShouldBe("Test.Subject");
            return Task.CompletedTask;
        });

        var pipeline = app.Build();

        var context = new DefaultHttpContext { Request =
            {
                ContentType =
                    charSet == null
                        ? "application/cloudevents+json"
                        : $"application/cloudevents+json;charset={charSet}",
                Body = dataContentType == null ?
                    MakeBody("{ \"type\": \"Test.Type\", \"subject\": \"Test.Subject\", \"data\": { \"name\":\"jimmy\" } }", encoding) :
                    MakeBody($"{{ \"datacontenttype\": \"{dataContentType}\", \"type\":\"Test.Type\", \"subject\": \"Test.Subject\", \"data\": {{ \"name\":\"jimmy\" }} }}", encoding)
            }
        };

        await pipeline.Invoke(context);
    }
        
    [Theory]
    [InlineData(null, null)] // assumes application/json + utf8
    [InlineData("application/json", null)] // assumes utf8
    [InlineData("application/json", "utf-8")]
    [InlineData("application/json", "UTF-8")]
    [InlineData("application/person+json", "UTF-16")] // arbitrary content type and charset
    public async Task InvokeAsync_ForwardsIncludedJsonPropertiesAsHeaders(string dataContentType, string charSet)
    {
        var encoding = charSet == null ? null : Encoding.GetEncoding(charSet);
        var serviceCollection = new ServiceCollection();
        var provider = serviceCollection.BuildServiceProvider();
            
        var app = new ApplicationBuilder(provider);
        app.UseCloudEvents(new CloudEventsMiddlewareOptions
        {
            ForwardCloudEventPropertiesAsHeaders = true,
            IncludedCloudEventPropertiesAsHeaders = new []{"type"}
        });

        // Do verification in the scope of the middleware
        app.Run(httpContext =>
        {
            httpContext.Request.ContentType.ShouldBe(dataContentType ?? "application/json");
            ReadBody(httpContext.Request.Body).ShouldBe("{\"name\":\"jimmy\"}");

            httpContext.Request.Headers.ShouldContainKey("Cloudevent.type");
            httpContext.Request.Headers["Cloudevent.type"].ToString().ShouldBe("Test.Type");
            httpContext.Request.Headers.ShouldNotContainKey("Cloudevent.subject");
            return Task.CompletedTask;
        });

        var pipeline = app.Build();

        var context = new DefaultHttpContext { Request =
            {
                ContentType =
                    charSet == null
                        ? "application/cloudevents+json"
                        : $"application/cloudevents+json;charset={charSet}",
                Body = dataContentType == null ?
                    MakeBody("{ \"type\": \"Test.Type\", \"subject\": \"Test.Subject\", \"data\": { \"name\":\"jimmy\" } }", encoding) :
                    MakeBody($"{{ \"datacontenttype\": \"{dataContentType}\", \"type\":\"Test.Type\", \"subject\": \"Test.Subject\", \"data\": {{ \"name\":\"jimmy\" }} }}", encoding)
            }
        };

        await pipeline.Invoke(context);
    }
        
    [Theory]
    [InlineData(null, null)] // assumes application/json + utf8
    [InlineData("application/json", null)] // assumes utf8
    [InlineData("application/json", "utf-8")]
    [InlineData("application/json", "UTF-8")]
    [InlineData("application/person+json", "UTF-16")] // arbitrary content type and charset
    public async Task InvokeAsync_DoesNotForwardExcludedJsonPropertiesAsHeaders(string dataContentType, string charSet)
    {
        var encoding = charSet == null ? null : Encoding.GetEncoding(charSet);
        var serviceCollection = new ServiceCollection();
        var provider = serviceCollection.BuildServiceProvider();
            
        var app = new ApplicationBuilder(provider);
        app.UseCloudEvents(new CloudEventsMiddlewareOptions
        {
            ForwardCloudEventPropertiesAsHeaders = true,
            ExcludedCloudEventPropertiesFromHeaders = new []{"type"}
        });

        // Do verification in the scope of the middleware
        app.Run(httpContext =>
        {
            httpContext.Request.ContentType.ShouldBe(dataContentType ?? "application/json");
            ReadBody(httpContext.Request.Body).ShouldBe("{\"name\":\"jimmy\"}");

            httpContext.Request.Headers.ShouldNotContainKey("Cloudevent.type");
            httpContext.Request.Headers.ShouldContainKey("Cloudevent.subject");
            httpContext.Request.Headers["Cloudevent.subject"].ToString().ShouldBe("Test.Subject");
            return Task.CompletedTask;
        });

        var pipeline = app.Build();

        var context = new DefaultHttpContext { Request =
            {
                ContentType =
                    charSet == null
                        ? "application/cloudevents+json"
                        : $"application/cloudevents+json;charset={charSet}",
                Body = dataContentType == null ?
                    MakeBody("{ \"type\": \"Test.Type\", \"subject\": \"Test.Subject\", \"data\": { \"name\":\"jimmy\" } }", encoding) :
                    MakeBody($"{{ \"datacontenttype\": \"{dataContentType}\", \"type\":\"Test.Type\", \"subject\": \"Test.Subject\", \"data\": {{ \"name\":\"jimmy\" }} }}", encoding)
            }
        };

        await pipeline.Invoke(context);
    }
                
    [Fact]
    public async Task InvokeAsync_ReplacesBodyNonJsonData()
    {
        // Our logic is based on the content-type, not the content.
        // Since this is for text-plain content, we're going to encode it as a JSON string
        // and store it in the data attribute - the middleware should JSON-decode it.
        const string input = "{ \"message\": \"hello, world\"}";
        var expected = input;

        var serviceCollection = new ServiceCollection();
        var provider = serviceCollection.BuildServiceProvider();
            
        var app = new ApplicationBuilder(provider);
        app.UseCloudEvents();

        // Do verification in the scope of the middleware
        app.Run(httpContext =>
        {
            httpContext.Request.ContentType.ShouldBe("text/plain");
            ReadBody(httpContext.Request.Body).ShouldBe(expected);
            return Task.CompletedTask;
        });

            
        var pipeline = app.Build();

        var context = new DefaultHttpContext { Request =
            {
                ContentType = "application/cloudevents+json", 
                Body = MakeBody($"{{ \"datacontenttype\": \"text/plain\", \"data\": {JsonSerializer.Serialize(input)} }}")
            }
        };

        await pipeline.Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_ReplacesBodyNonJsonData_ExceptWhenSuppressed()
    {
        // Our logic is based on the content-type, not the content. This test tests the old bad behavior.
        const string input = "{ \"message\": \"hello, world\"}";
        var expected = JsonSerializer.Serialize(input);

        var serviceCollection = new ServiceCollection();
        var provider = serviceCollection.BuildServiceProvider();
            
        var app = new ApplicationBuilder(provider);
        app.UseCloudEvents(new CloudEventsMiddlewareOptions() { SuppressJsonDecodingOfTextPayloads = true, });

        // Do verification in the scope of the middleware
        app.Run(httpContext =>
        {
            httpContext.Request.ContentType.ShouldBe("text/plain");
            ReadBody(httpContext.Request.Body).ShouldBe(expected);
            return Task.CompletedTask;
        });

            
        var pipeline = app.Build();

        var context = new DefaultHttpContext { Request =
            {
                ContentType = "application/cloudevents+json", 
                Body = MakeBody($"{{ \"datacontenttype\": \"text/plain\", \"data\": {JsonSerializer.Serialize(input)} }}")
            }
        };

        await pipeline.Invoke(context);
    }

    // This is a special case. S.T.Json will always output utf8, so we have to reinterpret the charset
    // of the datacontenttype.
    [Fact]
    public async Task InvokeAsync_ReplacesBodyJson_NormalizesPayloadCharset()
    {
        const string dataContentType = "application/person+json;charset=UTF-16";
        const string charSet = "UTF-16";
        var encoding = Encoding.GetEncoding(charSet);
        var serviceCollection = new ServiceCollection();
        var provider = serviceCollection.BuildServiceProvider();
            
        var app = new ApplicationBuilder(provider);
        app.UseCloudEvents();

        // Do verification in the scope of the middleware
        app.Run(httpContext =>
        {
            httpContext.Request.ContentType.ShouldBe("application/person+json");
            ReadBody(httpContext.Request.Body).ShouldBe("{\"name\":\"jimmy\"}");
            return Task.CompletedTask;
        });

        var pipeline = app.Build();

        var context = new DefaultHttpContext { Request =
            {
                ContentType = $"application/cloudevents+json;charset={charSet}", Body = MakeBody($"{{ \"datacontenttype\": \"{dataContentType}\", \"data\": {{ \"name\":\"jimmy\" }} }}", encoding)
            }
        };

        await pipeline.Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_ReadsBinaryData()
    {
        const string dataContentType = "application/octet-stream";
        var serviceCollection = new ServiceCollection();
        var provider = serviceCollection.BuildServiceProvider();
            
        var app = new ApplicationBuilder(provider);
        app.UseCloudEvents();
        var data = new byte[] { 1, 2, 3 };

        // Do verification in the scope of the middleware
        app.Run(httpContext =>
        {
            httpContext.Request.ContentType.ShouldBe(dataContentType);
            var bytes = new byte[httpContext.Request.Body.Length];
#if NET9_0
            httpContext.Request.Body.ReadExactly(bytes, 0, bytes.Length);
#else
                httpContext.Request.Body.Read(bytes, 0, bytes.Length);
#endif
            bytes.ShouldBe(data);
            return Task.CompletedTask;
        });

        var pipeline = app.Build();

        var context = new DefaultHttpContext { Request = { ContentType = "application/cloudevents+json" } };
        var base64Str = System.Convert.ToBase64String(data);

        context.Request.Body =
            MakeBody($"{{ \"datacontenttype\": \"{dataContentType}\", \"data_base64\": \"{base64Str}\"}}");

        await pipeline.Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_DataAndData64Set_ReturnsBadRequest()
    {
        const string dataContentType = "application/octet-stream";
        var serviceCollection = new ServiceCollection();
        var provider = serviceCollection.BuildServiceProvider();
            
        var app = new ApplicationBuilder(provider);
        app.UseCloudEvents();
        const string data = "{\"id\": \"1\"}";

        // Do verification in the scope of the middleware
        app.Run(httpContext =>
        {
            httpContext.Request.ContentType.ShouldBe("application/json");
            var body = ReadBody(httpContext.Request.Body);
            body.ShouldBe(data);
            return Task.CompletedTask;
        });

        var pipeline = app.Build();

        var context = new DefaultHttpContext { Request = { ContentType = "application/cloudevents+json" } };
        var bytes = Encoding.UTF8.GetBytes(data);
        var base64Str = System.Convert.ToBase64String(bytes);
        context.Request.Body =
            MakeBody($"{{ \"datacontenttype\": \"{dataContentType}\", \"data_base64\": \"{base64Str}\", \"data\": {data} }}");

        await pipeline.Invoke(context);
        context.Response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
    }

    private static Stream MakeBody(string text, Encoding encoding = null)
    {
        encoding ??= Encoding.UTF8;

        var stream = new MemoryStream();
        var bytes = encoding.GetBytes(text);
        stream.Write(bytes);
        stream.Seek(0L, SeekOrigin.Begin);
        return stream;
    }

    private static string ReadBody(Stream stream, Encoding encoding = null)
    {
        encoding ??= Encoding.UTF8;

        var bytes = new byte[stream.Length];
#if NET9_0
        stream.ReadExactly(bytes, 0, bytes.Length);
#else
            stream.Read(bytes, 0, bytes.Length);
#endif
        var str = encoding.GetString(bytes);
        return str;
    }
}