// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.AspNetCore.Test
{
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CloudEventsMiddlewareTest
    {
        [DataTestMethod]
        [DataRow("text/plain")]
        [DataRow("application/json")] // "binary" format
        [DataRow("application/cloudevents")] // no format
        [DataRow("application/cloudevents+xml")] // wrong format
        [DataRow("application/cloudevents-batch+json")] // we don't support batch
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

        [DataTestMethod]
        [DataRow(null, null)] // assumes application/json + utf8
        [DataRow("application/json", null)] // assumes utf8
        [DataRow("application/json", "utf-8")]
        [DataRow("application/json", "UTF-8")]
        [DataRow("application/person+json", "UTF-16")] // arbitrary content type and charset
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

        // This is a special case. S.T.Json will always output utf8, so we have to reinterpret the charset
        // of the datacontenttype.
        [TestMethod]
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

        private static Stream MakeBody(string text, Encoding encoding = null)
        {
            encoding ??= Encoding.UTF8;

            var stream = new MemoryStream();
            stream.Write(encoding.GetBytes(text));
            stream.Seek(0L, SeekOrigin.Begin);
            return stream;
        }

        private static string ReadBody(Stream stream, Encoding encoding = null)
        {
            encoding ??= Encoding.UTF8;

            var bytes = new byte[stream.Length];
            stream.Read(bytes, 0, bytes.Length);
            return encoding.GetString(bytes);
        }
    }
}