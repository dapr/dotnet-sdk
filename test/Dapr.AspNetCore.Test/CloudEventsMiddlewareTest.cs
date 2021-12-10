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

namespace Dapr.AspNetCore.Test
{
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
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
            var app = new ApplicationBuilder(null);
            app.UseCloudEvents();

            // Do verification in the scope of the middleware
            app.Run(httpContext =>
            {
                httpContext.Request.ContentType.Should().Be(contentType);
                ReadBody(httpContext.Request.Body).Should().Be("Hello, world!");
                return Task.CompletedTask;
            });

            var pipeline = app.Build();

            var context = new DefaultHttpContext();
            context.Request.ContentType = contentType;
            context.Request.Body = MakeBody("Hello, world!");

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
            var app = new ApplicationBuilder(null);
            app.UseCloudEvents();

            // Do verification in the scope of the middleware
            app.Run(httpContext =>
            {
                httpContext.Request.ContentType.Should().Be(dataContentType ?? "application/json");
                ReadBody(httpContext.Request.Body).Should().Be("{\"name\":\"jimmy\"}");
                return Task.CompletedTask;
            });

            var pipeline = app.Build();

            var context = new DefaultHttpContext();
            context.Request.ContentType = charSet == null ? "application/cloudevents+json" : $"application/cloudevents+json;charset={charSet}";
            context.Request.Body = dataContentType == null ?
                MakeBody("{ \"data\": { \"name\":\"jimmy\" } }", encoding) :
                MakeBody($"{{ \"datacontenttype\": \"{dataContentType}\", \"data\": {{ \"name\":\"jimmy\" }} }}", encoding);

            await pipeline.Invoke(context);
        }

        [Fact]
        public async Task InvokeAsync_ReplacesBodyNonJsonData()
        {
            // Our logic is based on the content-type, not the content.
            // Since this is for text-plain content, we're going to encode it as a JSON string
            // and store it in the data attribute - the middleware should JSON-decode it.
            var input = "{ \"message\": \"hello, world\"}";
            var expected = input;

            var app = new ApplicationBuilder(null);
            app.UseCloudEvents();

            // Do verification in the scope of the middleware
            app.Run(httpContext =>
            {
                httpContext.Request.ContentType.Should().Be("text/plain");
                ReadBody(httpContext.Request.Body).Should().Be(expected);
                return Task.CompletedTask;
            });

            
            var pipeline = app.Build();

            var context = new DefaultHttpContext();
            context.Request.ContentType = "application/cloudevents+json";
            context.Request.Body = MakeBody($"{{ \"datacontenttype\": \"text/plain\", \"data\": {JsonSerializer.Serialize(input)} }}");

            await pipeline.Invoke(context);
        }

        [Fact]
        public async Task InvokeAsync_ReplacesBodyNonJsonData_ExceptWhenSuppressed()
        {
            // Our logic is based on the content-type, not the content. This test tests the old bad behavior.
            var input = "{ \"message\": \"hello, world\"}";
            var expected = JsonSerializer.Serialize(input);

            var app = new ApplicationBuilder(null);
            app.UseCloudEvents(new CloudEventsMiddlewareOptions() { SuppressJsonDecodingOfTextPayloads = true, });

            // Do verification in the scope of the middleware
            app.Run(httpContext =>
            {
                httpContext.Request.ContentType.Should().Be("text/plain");
                ReadBody(httpContext.Request.Body).Should().Be(expected);
                return Task.CompletedTask;
            });

            
            var pipeline = app.Build();

            var context = new DefaultHttpContext();
            context.Request.ContentType = "application/cloudevents+json";
            context.Request.Body = MakeBody($"{{ \"datacontenttype\": \"text/plain\", \"data\": {JsonSerializer.Serialize(input)} }}");

            await pipeline.Invoke(context);
        }

        // This is a special case. S.T.Json will always output utf8, so we have to reinterpret the charset
        // of the datacontenttype.
        [Fact]
        public async Task InvokeAsync_ReplacesBodyJson_NormalizesPayloadCharset()
        {
            var dataContentType = "application/person+json;charset=UTF-16";
            var charSet = "UTF-16";
            var encoding = Encoding.GetEncoding(charSet);
            var app = new ApplicationBuilder(null);
            app.UseCloudEvents();

            // Do verification in the scope of the middleware
            app.Run(httpContext =>
            {
                httpContext.Request.ContentType.Should().Be("application/person+json");
                ReadBody(httpContext.Request.Body).Should().Be("{\"name\":\"jimmy\"}");
                return Task.CompletedTask;
            });

            var pipeline = app.Build();

            var context = new DefaultHttpContext();
            context.Request.ContentType = $"application/cloudevents+json;charset={charSet}";
            context.Request.Body =
                MakeBody($"{{ \"datacontenttype\": \"{dataContentType}\", \"data\": {{ \"name\":\"jimmy\" }} }}", encoding);

            await pipeline.Invoke(context);
        }

        [Fact]
        public async Task InvokeAsync_ReadsBinaryData()
        {
            var dataContentType = "application/octet-stream";
            var app = new ApplicationBuilder(null);
            app.UseCloudEvents();
            var data = new byte[] { 1, 2, 3 };

            // Do verification in the scope of the middleware
            app.Run(httpContext =>
            {
                httpContext.Request.ContentType.Should().Be(dataContentType);
                var bytes = new byte[httpContext.Request.Body.Length];
                httpContext.Request.Body.Read(bytes, 0, bytes.Length);
                bytes.Should().Equal(data);
                return Task.CompletedTask;
            });

            var pipeline = app.Build();

            var context = new DefaultHttpContext();
            context.Request.ContentType = "application/cloudevents+json";
            var base64Str = System.Convert.ToBase64String(data);

            context.Request.Body =
                MakeBody($"{{ \"datacontenttype\": \"{dataContentType}\", \"data_base64\": \"{base64Str}\"}}");

            await pipeline.Invoke(context);
        }

        [Fact]
        public async Task InvokeAsync_DataAndData64Set_ReturnsBadRequest()
        {
            var dataContentType = "application/octet-stream";
            var app = new ApplicationBuilder(null);
            app.UseCloudEvents();
            var data = "{\"id\": \"1\"}";

            // Do verification in the scope of the middleware
            app.Run(httpContext =>
            {
                httpContext.Request.ContentType.Should().Be("application/json");
                var body = ReadBody(httpContext.Request.Body);
                body.Should().Equals(data);
                return Task.CompletedTask;
            });

            var pipeline = app.Build();

            var context = new DefaultHttpContext();
            context.Request.ContentType = "application/cloudevents+json";
            var bytes = Encoding.UTF8.GetBytes(data);
            var base64Str = System.Convert.ToBase64String(bytes);
            context.Request.Body =
                MakeBody($"{{ \"datacontenttype\": \"{dataContentType}\", \"data_base64\": \"{base64Str}\", \"data\": {data} }}");

            await pipeline.Invoke(context);
            context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
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
            stream.Read(bytes, 0, bytes.Length);
            var str = encoding.GetString(bytes);
            return str;
        }
    }
}
