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

namespace Dapr
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.WebUtilities;
    using Microsoft.Extensions.Primitives;
    using Microsoft.Net.Http.Headers;

    internal class CloudEventsMiddleware
    {
        private const string ContentType = "application/cloudevents+json";
        private readonly RequestDelegate next;
        private readonly CloudEventsMiddlewareOptions options;

        public CloudEventsMiddleware(RequestDelegate next, CloudEventsMiddlewareOptions options)
        {
            this.next = next;
            this.options = options;
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

            // Check whether to use data or data_base64 as per https://github.com/cloudevents/spec/blob/v1.0.1/json-format.md#31-handling-of-data
            var isDataSet = json.TryGetProperty("data", out var data);
            var isBinaryDataSet = json.TryGetProperty("data_base64", out var binaryData);

            if (isDataSet && isBinaryDataSet)
            {
                httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }
            else if (isDataSet)
            {
                contentType = this.GetDataContentType(json, out var isJson);

                // If the value is anything other than a JSON string, treat it as JSON. Cloud Events requires
                // non-JSON text to be enclosed in a JSON string.
                isJson |= data.ValueKind != JsonValueKind.String;

                body = new MemoryStream();
                if (isJson || options.SuppressJsonDecodingOfTextPayloads)
                {
                    // Rehydrate body from JSON payload
                    await JsonSerializer.SerializeAsync<JsonElement>(body, data);
                }
                else
                {
                    // Rehydrate body from contents of the string
                    var text = data.GetString();
                    using var writer = new HttpResponseStreamWriter(body, Encoding.UTF8);
                    writer.Write(text);
                }

                body.Seek(0L, SeekOrigin.Begin);
            }
            else if (isBinaryDataSet)
            {
                // As per the spec, if the implementation determines that the type of data is Binary,
                // the value MUST be represented as a JSON string expression containing the Base64 encoded
                // binary value, and use the member name data_base64 to store it inside the JSON object.
                var decodedBody = binaryData.GetBytesFromBase64();
                body = new MemoryStream(decodedBody);
                body.Seek(0L, SeekOrigin.Begin);
                contentType = this.GetDataContentType(json, out _);
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

        private string GetDataContentType(JsonElement json, out bool isJson)
        {
            string contentType;
            if (json.TryGetProperty("datacontenttype", out var dataContentType) &&
                dataContentType.ValueKind == JsonValueKind.String && 
                MediaTypeHeaderValue.TryParse(dataContentType.GetString(), out var parsed))
            {
                contentType = dataContentType.GetString();
                isJson = 
                    parsed.MediaType.Equals( "application/json", StringComparison.Ordinal) ||
                    parsed.Suffix.EndsWith("+json", StringComparison.Ordinal);

                // Since S.T.Json always outputs utf-8, we may need to normalize the data content type
                // to remove any charset information. We generally just assume utf-8 everywhere, so omitting
                // a charset is a safe bet.
                if (contentType.Contains("charset"))
                {
                    parsed.Charset = StringSegment.Empty;
                    contentType = parsed.ToString();
                }
            }
            else
            {
                // assume JSON is not specified.
                contentType = "application/json";
                isJson = true;
            }

            return contentType;
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

            charSet = parsed.Charset.Length > 0 ? parsed.Charset.Value : "UTF-8";
            return true;
        }
    }
}
