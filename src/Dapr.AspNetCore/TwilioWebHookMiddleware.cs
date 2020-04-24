// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.CookiePolicy;
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.WebUtilities;
    internal class TwilioWebHookMiddleware
    {
        private const string ContentType = "application/x-www-form-urlencoded";
        private readonly RequestDelegate next;

        public TwilioWebHookMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public Task InvokeAsync(HttpContext httpContext)
        {
            // This middleware converts Form URL Encoded POST (e.g. Twilio, but can be used for any form url encoding) to JSON which
            // the Actors are limited to ...
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
            string formURLParameters = string.Empty;

            using (StreamReader stream = new StreamReader(httpContext.Request.Body, Encoding.GetEncoding(charSet)))
            {
                formURLParameters = await stream.ReadToEndAsync();
            }

            var query = QueryHelpers.ParseQuery(formURLParameters);

            //convert query to dictionary, which enables clean serialization to json
            var jsonOptions = new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
            };

            var formEncodedKeyPairs = query.ToDictionary(pKey => pKey.Key, pValue =>
            {
                if (pValue.Value.Count <= 1)
                    return (object)pValue.Value.FirstOrDefault();
                else
                    return (object)JsonSerializer.Serialize(pValue.Value, jsonOptions); ;
            });

            var jsonFromFormInJson = JsonSerializer.Serialize(formEncodedKeyPairs, jsonOptions);

            Stream originalBody;
            Stream body;


            string originalContentType;
            string contentType = "application/json";

            originalBody = httpContext.Request.Body;
            originalContentType = httpContext.Request.ContentType;

            try
            {
                byte[] byteArray = Encoding.UTF8.GetBytes(jsonFromFormInJson);
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
