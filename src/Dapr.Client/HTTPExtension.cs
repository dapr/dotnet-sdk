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
        public Dictionary<string, string> QueryString { get; set; }

        /// <summary>
        /// This represents a collection of HTTP headers.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; }
    }
}
