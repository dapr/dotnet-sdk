// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http.Headers;
    using System.Runtime.Serialization.Json;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.WebUtilities;
    using Dapr.Utility;
    

    internal class FormURLEncodedMiddleware
    {
        private const string ContentType = "application/x-www-form-urlencoded";
        private readonly RequestDelegate next;

        public FormURLEncodedMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        [JsonConverter(typeof(DictionaryConverter))]
        private Dictionary<string, object> FormKeyValuePairs { get; set; } = new Dictionary<string, object>();

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


            var formkeyValuePairs = httpContext.Request.Form;

            foreach (var item in formkeyValuePairs)
            {
                //  System.Diagnostics.Debug.Print($"key:{item.Key}");
                //  System.Diagnostics.Debug.Print($"key:{item.Value}");
                //  System.Diagnostics.Debug.Print($"-----------");

                var key = Uri.UnescapeDataString(item.Key).Replace("+", " ");
                var value = Uri.UnescapeDataString(item.Value).Replace("+", " ");
                FormKeyValuePairs.Add(key, value);
            }

            /*
            * Just a left over sample on converting a form url encoded string
            * twiml = twiml.Replace("utf-16", "utf-8");
            */

            Stream originalBody;
            Stream body;

            string originalContentType;
            string contentType;

            contentType = "application/json";

            originalBody = httpContext.Request.Body;
            originalContentType = httpContext.Request.ContentType;


            try
            {
                var jsoncrap = JsonSerializer.Serialize(FormKeyValuePairs);
                //jsoncrap.Replace("value1", convertFormCollection["id"].ToString());
                //jsoncrap.Replace("value2", convertFormCollection["amount"].ToString());
                //var formtojson = JsonSerializer.Serialize(convertFormCollection);
                System.Diagnostics.Debug.Print($"jsonjunk:{jsoncrap}");

                // convert string to stream
                byte[] byteArray = Encoding.UTF8.GetBytes(jsoncrap);
                body = new MemoryStream(byteArray);

                httpContext.Request.Body = body;
                httpContext.Request.ContentType = contentType;

                await this.next(httpContext);
            }
            finally
            {
                httpContext.Request.ContentType = originalContentType;
                httpContext.Request.Body = originalBody;
            }
            System.Diagnostics.Debug.Print("leaving");
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
