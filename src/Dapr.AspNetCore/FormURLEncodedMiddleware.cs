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
    using System.Net.Http;
    using System.Linq;
    using System.Collections.Specialized;
    

    internal class FormURLEncodedMiddleware
    {
        private const string ContentType = "application/x-www-form-urlencoded";
        private readonly RequestDelegate next;
        private static readonly HttpClient client = new HttpClient();

        public FormURLEncodedMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        //https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-converters-how-to
        //[JsonConverter(typeof(DictionaryConverter))]
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
            if (this.MatchesContentType(httpContext, out var charSet)==false || httpContext.Request.Headers.Keys.Contains("TwilioURLConvert"))
            {
                return this.next(httpContext);
            }

            return this.ProcessBodyAsync(httpContext, charSet);
        }

public static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage req)
    {
        HttpRequestMessage clone = new HttpRequestMessage(req.Method, req.RequestUri);

        // Copy the request's content (via a MemoryStream) into the cloned object
        var ms = new MemoryStream();
        if (req.Content != null)
        {
            await req.Content.CopyToAsync(ms).ConfigureAwait(false);
            ms.Position = 0;
            clone.Content = new StreamContent(ms);

            // Copy the content headers
            if (req.Content.Headers != null)
                foreach (var h in req.Content.Headers)
                    clone.Content.Headers.Add(h.Key, h.Value);
        }


        clone.Version = req.Version;

        foreach (KeyValuePair<string, object> prop in req.Properties)
            clone.Properties.Add(prop);

        foreach (KeyValuePair<string, IEnumerable<string>> header in req.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }
       

        private async Task ProcessBodyAsync(HttpContext httpContext, string charSet)
        {
            // //copy headers to request
             client.DefaultRequestHeaders.Accept.Clear();
             client.DefaultRequestHeaders.Accept
                 .Add(new MediaTypeWithQualityHeaderValue(ContentType));//ACCEPT header
             client.BaseAddress = new Uri( httpContext.Request.Path.Value);

            //var incomingHeaders  = httpContext.Request.Headers;
            var outgoingrequest = new HttpRequestMessage()
                {
                    RequestUri = new Uri(client.BaseAddress,"TwilioPost"),
                    Method = new HttpMethod(httpContext.Request.Method)
                };
                
            // CopyHeaders(outgoingrequest, outgoingrequest.Headers, incomingHeaders);
            
            outgoingrequest.Content = new StreamContent(httpContext.Request.Body);

             // Copy the content headers
            if (httpContext.Request.Headers != null)
            {
                foreach (var h in httpContext.Request.Headers)
                {
                    var KeyInHeader = h.Key;
                    bool AlreadyExistsTest = false; ;
                    try
                    {
                        AlreadyExistsTest = outgoingrequest.Content.Headers.Any(pKeyValuePair => pKeyValuePair.Key.Equals(h.Key)==false);
                    }
                    catch(Exception ex)
                    {
                        var whatthefuck = ex.Message;
                    }
                    if (AlreadyExistsTest == false)
                        outgoingrequest.Content.Headers.Add(h.Key, h.Value.ToList());
                }
            }

            outgoingrequest.Content.Headers.Add("TwilioURLConvert","True");
            var response = await client.SendAsync(outgoingrequest);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            Stream originalBody;
            Stream body;

            string originalContentType;
            string contentType;

            contentType = "application/json";

            originalBody = httpContext.Request.Body;
            originalContentType = httpContext.Request.ContentType;


            try
            {
                // convert string to stream
                byte[] byteArray = Encoding.UTF8.GetBytes(jsonResponse);
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
