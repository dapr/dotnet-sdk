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
    using System.Diagnostics;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.Mvc;
    using System.Security.Cryptography;

    internal class FormURLEncodedMiddleware
    {
        private const string FormURLContentType = "application/x-www-form-urlencoded";
        private const string HeaderFlagName = "FromFormEncodingToJson";
        private readonly RequestDelegate next;
        private static readonly HttpClient client = new HttpClient();
        private readonly string FormFormControllerName = string.Empty;

        public FormURLEncodedMiddleware(RequestDelegate next, string options)
        {
            this.next = next;
            if (string.IsNullOrWhiteSpace(options))
                System.Diagnostics.Debugger.Break();
            this.FormFormControllerName = options;
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

            //todo: really should see if the URI has the FromFormControllerName on the tail end, and then allow it to pass
            if (this.MatchesContentType(httpContext, out var charSet) == false || httpContext.Request.Headers.Keys.Contains(HeaderFlagName) || httpContext.Request.Method.ToLower() == "get")
            {
                return this.next(httpContext);
            }

            return this.ProcessBodyAsync(httpContext, charSet);
        }


        private async Task ProcessBodyAsync(HttpContext httpContext, string charSet)
        {
            //ensure we set this only once
            if (client.BaseAddress == null)
            {
                // //copy headers to request
                //client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept
                    .Add(new MediaTypeWithQualityHeaderValue(FormURLContentType));//ACCEPT header
                client.BaseAddress = GetBaseURLFromContext(httpContext);
            }

            var outgoingrequest = new HttpRequestMessage()
            {
                RequestUri = new Uri(client.BaseAddress, FormFormControllerName),
                Method = new HttpMethod(httpContext.Request.Method)
            };

            Debug.WriteLine($"Sending request to: {outgoingrequest.RequestUri}");
            Debug.WriteLine($"Content-Type Accept Header:{client.DefaultRequestHeaders.Accept}");

            //content must be created first to enable access to headers
            outgoingrequest.Content = new StreamContent(httpContext.Request.Body);
            outgoingrequest.Content.Headers.ContentType = new MediaTypeHeaderValue(FormURLContentType);
            outgoingrequest.Content.Headers.ContentLength = httpContext.Request.Headers.ContentLength;


            //// Copy the incoming headers to the outgoing request
            //if (httpContext.Request.Headers != null)
            //{
            //    foreach (var h in httpContext.Request.Headers)
            //    {
            //        if (outgoingrequest.Content.Headers.Contains(h.Key) == false)
            //           outgoingrequest.Content.Headers.Add(h.Key, h.Value.ToList());
            //    }
            //}

            //send to Action on Controller that will convert FormURLEncoded to JSON using  the class defined in method signature
            outgoingrequest.Content.Headers.Add(HeaderFlagName, "True");

            HttpResponseMessage response = null;

            try
            {
                response = await client.SendAsync(outgoingrequest);
            }
            catch (Exception ex)
            {
                string exMsg = ex.Message;
                throw;
            }

            string jsonResponse = string.Empty;
            if (response.IsSuccessStatusCode)
                jsonResponse = await response.Content.ReadAsStringAsync();
            else
                throw new Exception("Post to Twilio FormURL was not successful");

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

                Debug.WriteLine($"sending Json Response {jsonResponse}");
                await this.next(httpContext);
            }
            catch (Exception ex)
            {
                var x = ex.Message;
                throw;
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
            if (!httpContext.Request.ContentType.StartsWith(FormURLContentType))
            {
                charSet = null;
                return false;
            }

            if (!MediaTypeHeaderValue.TryParse(httpContext.Request.ContentType, out var parsed))
            {
                charSet = null;
                return false;
            }

            if (parsed.MediaType != FormURLContentType)
            {
                charSet = null;
                return false;
            }

            charSet = parsed.CharSet ?? "UTF-8";
            return true;
        }

        //found here: https://www.hanselman.com/blog/DetectingThatANETCoreAppIsRunningInADockerContainerAndSkippableFactsInXUnit.aspx
        private bool InDocker { get { return Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true"; } }

        public Uri GetBaseURLFromContext(HttpContext httpContext)
        {
            string AppBaseUrl = string.Empty;
            
            if (InDocker)
                AppBaseUrl = $"http://localhost";
            else
                AppBaseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}";


            AppBaseUrl = $"http://localhost:60022";
            return new Uri(AppBaseUrl);

        }

        //todo: can this be change to handle the headers better?
        //todo: httpRequestMessage is old, and not HttpRequest which is new (.net core)
        //private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage req)
        //{
        //    HttpRequestMessage clone = new HttpRequestMessage(req.Method, req.RequestUri);

        //    // Copy the request's content (via a MemoryStream) into the cloned object
        //    var ms = new MemoryStream();
        //    if (req.Content != null)
        //    {
        //        await req.Content.CopyToAsync(ms).ConfigureAwait(false);
        //        ms.Position = 0;
        //        clone.Content = new StreamContent(ms);

        //        // Copy the content headers
        //        if (req.Content.Headers != null)
        //            foreach (var h in req.Content.Headers)
        //                clone.Content.Headers.Add(h.Key, h.Value);
        //    }


        //    clone.Version = req.Version;

        //    foreach (KeyValuePair<string, object> prop in req.Properties)
        //        clone.Properties.Add(prop);

        //    foreach (KeyValuePair<string, IEnumerable<string>> header in req.Headers)
        //        clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        //    return clone;
        //}
    }
}
