// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    using System.Collections.Generic;
    using System.Net.Http;

    /// <summary>
    /// Represents options for invoking a reciever over HTTP. Allows specifying HTTP-specific semantics of the
    /// request like the HTTP method, query string, and headers.
    /// </summary>
    public class HttpInvocationOptions
    {
        /// <summary>
        /// Initializes a new instance of <see cref="HttpInvocationOptions" />.
        /// </summary>
        public HttpInvocationOptions()
        {
            this.Method = HttpMethod.Post;
            this.QueryString = new Dictionary<string, string>();
            this.Headers = new Dictionary<string, string>();
        }

        /// <summary>
        /// The HTTP Content-Type
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// This is the HTTP method.
        /// </summary>
        public HttpMethod Method { get; set; }

        /// <summary>
        /// This represents a collection of query strings.
        /// </summary>
        public IDictionary<string, string> QueryString { get; set; }

        /// <summary>
        /// This represents a collection of HTTP headers.
        /// </summary>
        public IDictionary<string, string> Headers { get; set; }

        #region Fluent Methods

        /// <summary>
        /// This convenience method allow constructing a <see cref="HttpMethod.Get"/> request.
        /// </summary>
        /// <returns>Instance of <see cref="HttpInvocationOptions"/></returns>
        public static HttpInvocationOptions UsingGet() => new HttpInvocationOptions()
        {
            Method = HttpMethod.Get
        };

        /// <summary>
        /// This convenience method allow constructing a <see cref="HttpMethod.Post"/> request.
        /// </summary>
        /// <returns>Instance of <see cref="HttpInvocationOptions"/></returns>
        public static HttpInvocationOptions UsingPost() => new HttpInvocationOptions()
        {
            Method = HttpMethod.Post
        };

        /// <summary>
        /// This convenience method allow constructing a <see cref="HttpMethod.Put"/> request.
        /// </summary>
        /// <returns>Instance of <see cref="HttpInvocationOptions"/></returns>
        public static HttpInvocationOptions UsingPut() => new HttpInvocationOptions()
        {
            Method = HttpMethod.Put
        };

        /// <summary>
        /// This convenience method allow constructing a <see cref="HttpMethod.Delete"/> request.
        /// </summary>
        /// <returns>Instance of <see cref="HttpInvocationOptions"/></returns>
        public static HttpInvocationOptions UsingDelete() => new HttpInvocationOptions()
        {
            Method = HttpMethod.Delete
        };

        /// <summary>
        /// This convenience method allow constructing an HTTP <c>CONNECT</c> request.
        /// </summary>
        /// <returns>Instance of <see cref="HttpInvocationOptions"/></returns>
        public static HttpInvocationOptions UsingConnect() => new HttpInvocationOptions()
        {
            // CONNECT is extremely specialized and uncommon - .NET doesn't define a constant for it.
            Method = new HttpMethod("CONNECT") 
        };

        /// <summary>
        /// This convenience method allow constructing a <see cref="HttpMethod.Options"/> request.
        /// </summary>
        /// <returns>Instance of <see cref="HttpInvocationOptions"/></returns>
        public static HttpInvocationOptions UsingOptions() => new HttpInvocationOptions()
        {
            Method = HttpMethod.Options
        };

        /// <summary>
        /// This convenience method allow constructing a <see cref="HttpMethod.Head"/> request
        /// </summary>
        /// <returns>Instance of <see cref="HttpInvocationOptions"/></returns>
        public static HttpInvocationOptions UsingHead() => new HttpInvocationOptions()
        {
            Method = HttpMethod.Head
        };

        /// <summary>
        /// This convenience method allow constructing a <see cref="HttpMethod.Trace"/> request
        /// </summary>
        /// <returns>Instance of <see cref="HttpInvocationOptions"/></returns>
        public static HttpInvocationOptions UsingTrace() => new HttpInvocationOptions()
        {
            Method = HttpMethod.Trace
        };

        /// <summary>
        /// This convenience method allow constructing a request with Query Params
        /// </summary>
        /// <param name="param">The query parameter you want to add</param>
        /// <returns>An updated instance of the same <see cref="HttpInvocationOptions"/></returns>
        public HttpInvocationOptions WithQueryParam(KeyValuePair<string, string> param)
        {
            this.QueryString.Add(param.Key, param.Value);
            return this;
        }

        /// <summary>
        /// This convenience method allow constructing a request with Query Params
        /// </summary>
        /// <param name="name">Name of the query parameter</param>
        /// <param name="value">Value of the query parameter</param>
        /// <returns>An updated instance of the same <see cref="HttpInvocationOptions"/></returns>
        public HttpInvocationOptions WithQueryParam(string name, string value)
        {
            this.QueryString.Add(name, value);
            return this;
        }

        /// <summary>
        /// This convenience method allow constructing a request with headers
        /// </summary>
        /// <param name="header">The header name and value you want to add</param>
        /// <returns>An updated instance of the same <see cref="HttpInvocationOptions"/></returns>
        public HttpInvocationOptions WithHeader(KeyValuePair<string, string> header)
        {
            this.Headers.Add(header.Key, header.Value);
            return this;
        }

        /// <summary>
        /// This convenience method allow constructing a request with headers
        /// </summary>
        /// <param name="name">Name of the header</param>
        /// <param name="value">Value of the header</param>
        /// <returns>An updated instance of the same <see cref="HttpInvocationOptions"/></returns>
        public HttpInvocationOptions WithHeader(string name, string value)
        {
            this.Headers.Add(name, value);
            return this;
        }

        /// <summary>
        /// Uses <see cref="Constants.ContentTypeApplicationJson"/> as the content type for the request>
        /// </summary>
        /// <returns>An updated instance of the same <see cref="HttpInvocationOptions"/></returns>
        public HttpInvocationOptions WithJsonContentType()
        {
            this.ContentType = Constants.ContentTypeApplicationJson;
            return this;
        }

        /// <summary>
        /// Uses <see cref="Constants.ContentTypeApplicationGrpc"/> as the content type for the request>
        /// </summary>
        /// <returns>An updated instance of the same <see cref="HttpInvocationOptions"/></returns>
        public HttpInvocationOptions WithGrpcContentType()
        {
            this.ContentType = Constants.ContentTypeApplicationGrpc;
            return this;
        }

        #endregion
    }
}
