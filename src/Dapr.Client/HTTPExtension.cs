// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client.Http
{
    using System.Collections.Generic;

    /// <summary>
    /// This class is only needed if the app you are calling is listening on HTTP.
    /// It contains propertes that represent data may be populated for an HTTP receiver.
    /// </summary>
    public class HTTPExtension
    {
        /// <summary>
        /// The constructor.
        /// </summary>
        public HTTPExtension()
        {
            this.Verb = HTTPVerb.Post;
            this.QueryString = new Dictionary<string, string>();
            this.Headers = new Dictionary<string, string>();
        }

        /// <summary>
        /// The HTTP Content-Type
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// This is the HTTP verb.
        /// </summary>
        public HTTPVerb Verb { get; set; }

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
        /// This convenience method allow constructing a <see cref="HTTPVerb.Get"/> request
        /// </summary>
        /// <returns>Instance of <see cref="HTTPExtension"/></returns>
        public static HTTPExtension UsingGet() => new HTTPExtension()
        {
            Verb = HTTPVerb.Get
        };

        /// <summary>
        /// This convenience method allow constructing a <see cref="HTTPVerb.Post"/> request
        /// </summary>
        /// <returns>Instance of <see cref="HTTPExtension"/></returns>
        public static HTTPExtension UsingPost() => new HTTPExtension()
        {
            Verb = HTTPVerb.Post
        };

        /// <summary>
        /// This convenience method allow constructing a <see cref="HTTPVerb.Put"/> request
        /// </summary>
        /// <returns>Instance of <see cref="HTTPExtension"/></returns>
        public static HTTPExtension UsingPut() => new HTTPExtension()
        {
            Verb = HTTPVerb.Put
        };

        /// <summary>
        /// This convenience method allow constructing a <see cref="HTTPVerb.Delete"/> request
        /// </summary>
        /// <returns>Instance of <see cref="HTTPExtension"/></returns>
        public static HTTPExtension UsingDelete() => new HTTPExtension()
        {
            Verb = HTTPVerb.Delete
        };

        /// <summary>
        /// This convenience method allow constructing a <see cref="HTTPVerb.Connect"/> request
        /// </summary>
        /// <returns>Instance of <see cref="HTTPExtension"/></returns>
        public static HTTPExtension UsingConnect() => new HTTPExtension()
        {
            Verb = HTTPVerb.Connect
        };

        /// <summary>
        /// This convenience method allow constructing a <see cref="HTTPVerb.Options"/> request
        /// </summary>
        /// <returns>Instance of <see cref="HTTPExtension"/></returns>
        public static HTTPExtension UsingOptions() => new HTTPExtension()
        {
            Verb = HTTPVerb.Options
        };

        /// <summary>
        /// This convenience method allow constructing a <see cref="HTTPVerb.Head"/> request
        /// </summary>
        /// <returns>Instance of <see cref="HTTPExtension"/></returns>
        public static HTTPExtension UsingHead() => new HTTPExtension()
        {
            Verb = HTTPVerb.Head
        };

        /// <summary>
        /// This convenience method allow constructing a <see cref="HTTPVerb.Trace"/> request
        /// </summary>
        /// <returns>Instance of <see cref="HTTPExtension"/></returns>
        public static HTTPExtension UsingTrace() => new HTTPExtension()
        {
            Verb = HTTPVerb.Trace
        };

        /// <summary>
        /// This convenience method allow constructing a request with Query Params
        /// </summary>
        /// <param name="param">The query parameter you want to add</param>
        /// <returns>An updated instance of the same <see cref="HTTPExtension"/></returns>
        public HTTPExtension WithQueryParam(KeyValuePair<string, string> param)
        {
            this.QueryString.Add(param.Key, param.Value);
            return this;
        }

        /// <summary>
        /// This convenience method allow constructing a request with Query Params
        /// </summary>
        /// <param name="name">Name of the query parameter</param>
        /// <param name="value">Value of the query parameter</param>
        /// <returns>An updated instance of the same <see cref="HTTPExtension"/></returns>
        public HTTPExtension WithQueryParam(string name, string value)
        {
            this.QueryString.Add(name, value);
            return this;
        }

        /// <summary>
        /// This convenience method allow constructing a request with headers
        /// </summary>
        /// <param name="header">The header name and value you want to add</param>
        /// <returns>An updated instance of the same <see cref="HTTPExtension"/></returns>
        public HTTPExtension WithHeader(KeyValuePair<string, string> header)
        {
            this.Headers.Add(header.Key, header.Value);
            return this;
        }

        /// <summary>
        /// This convenience method allow constructing a request with headers
        /// </summary>
        /// <param name="name">Name of the header</param>
        /// <param name="value">Value of the header</param>
        /// <returns>An updated instance of the same <see cref="HTTPExtension"/></returns>
        public HTTPExtension WithHeader(string name, string value)
        {
            this.Headers.Add(name, value);
            return this;
        }

        /// <summary>
        /// Uses <see cref="Constants.ContentTypeApplicationJson"/> as the content type for the request>
        /// </summary>
        /// <returns>An updated instance of the same <see cref="HTTPExtension"/></returns>
        public HTTPExtension WithJsonContentType()
        {
            this.ContentType = Constants.ContentTypeApplicationJson;
            return this;
        }

        /// <summary>
        /// Uses <see cref="Constants.ContentTypeApplicationGrpc"/> as the content type for the request>
        /// </summary>
        /// <returns>An updated instance of the same <see cref="HTTPExtension"/></returns>
        public HTTPExtension WithGrpcContentType()
        {
            this.ContentType = Constants.ContentTypeApplicationGrpc;
            return this;
        }

        #endregion
    }
}
