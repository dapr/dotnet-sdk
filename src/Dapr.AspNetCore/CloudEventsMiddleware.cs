// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using System;
    using System.IO;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.WebUtilities;

    internal class CloudEventsMiddleware
    {
        private const string ContentType = "application/cloudevents+json";
        private readonly RequestDelegate next;

        public CloudEventsMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public Task InvokeAsync(HttpContext httpContext)
        {
            // This middleware unwraps any requests with a cloud events (JSON) content type
            // and replaces the request body + request content type so that it can be read by a
            // non-cloud-events-aware piece of code.
            //
            // This corresponds to cloud events in the *structured* format:
            // https://github.com/cloudevents/spec/blob/master/http-transport-binding.md#13-content-modes
            //
            // For *binary* format, we don't have to do anything
            //
            // We don't support batching.
            //
            // The philosophy here is that we don't report an error for things we don't support, because
            // that would block someone from implementing their own support for it. We only report an error
            // when something we do support isn't correct.
            if (!this.MatchesContentType(httpContext, out var charSet))
            {
                return this.next(httpContext);
            }

            return this.ProcessBodyAsync(httpContext, charSet);
        }

        private async Task ProcessBodyAsync(HttpContext httpContext, string charSet)
        {
            JsonElement json;
            if (string.Equals(charSet, Encoding.UTF8.WebName, StringComparison.OrdinalIgnoreCase))
            {
                json = await JsonSerializer.DeserializeAsync<JsonElement>(httpContext.Request.Body);
            }
            else
            {
                using (var reader = new HttpRequestStreamReader(httpContext.Request.Body, Encoding.GetEncoding(charSet)))
                {
                    var text = await reader.ReadToEndAsync();
                    json = JsonSerializer.Deserialize<JsonElement>(text);
                }
            }

            Stream originalBody;
            Stream body;

            string originalContentType;
            string contentType;

            // Data is optional.
            if (json.TryGetProperty("data", out var data))
            {
                body = new MemoryStream();
                await JsonSerializer.SerializeAsync<JsonElement>(body, data);
                body.Seek(0L, SeekOrigin.Begin);

                if (json.TryGetProperty("datacontenttype", out var dataContentType) &&
                    dataContentType.ValueKind == JsonValueKind.String)
                {
                    contentType = dataContentType.GetString();

                    // Since S.T.Json always outputs utf-8, we may need to normalize the data content type
                    // to remove any charset information. We generally just assume utf-8 everywhere, so omitting
                    // a charset is a safe bet.
                    if (contentType.Contains("charset") && MediaTypeHeaderValue.TryParse(contentType, out var parsed))
                    {
                        parsed.CharSet = null;
                        contentType = parsed.ToString();
                    }
                }
                else
                {
                    // assume JSON is not specified.
                    contentType = "application/json";
                }
            }
            else
            {
                body = new MemoryStream();
                contentType = null;
            }

            originalBody = httpContext.Request.Body;
            originalContentType = httpContext.Request.ContentType;

            try
            {
                httpContext.Request.Body = body;
                httpContext.Request.ContentType = contentType;

                await this.next(httpContext);
            }
            finally
            {
                httpContext.Request.ContentType = originalContentType;
                httpContext.Request.Body = originalBody;
            }
        }

        private bool MatchesContentType(HttpContext httpContext, out string charSet)
        {
            if (httpContext.Request.ContentType == null)
            {
                charSet = null;
                return false;
            }

            // Handle cases where the content type includes additional parameters like charset.
            // Doing the string comparison up front so we can avoid allocation.
            if (!httpContext.Request.ContentType.StartsWith(ContentType))
            {
                charSet = null;
                return false;
            }

            if (!MediaTypeHeaderValue.TryParse(httpContext.Request.ContentType, out var parsed))
            {
                charSet = null;
                return false;
            }

            if (parsed.MediaType != ContentType)
            {
                charSet = null;
                return false;
            }

            charSet = parsed.CharSet ?? "UTF-8";
            return true;
        }
    }
}