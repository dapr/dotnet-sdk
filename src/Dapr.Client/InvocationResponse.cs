// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    using System.Net;
    using System.Collections.Generic;
    using Dapr.Client;
    using Dapr.Client.Http;

    /// <summary>
    /// Represents a response returned by service invocation
    /// </summary>
    /// <typeparam name="TResponse">Data type of the response.</typeparam>
    public sealed class InvocationResponse<TResponse>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvocationResponse{TResponse}"/> class.
        /// </summary>
        public InvocationResponse()
        {
        }

        /// <summary>
        /// Gets or sets the response body.
        /// </summary>
        public TResponse Body { get; set; }

        /// <summary>
        /// Gets or sets the content type.
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Gets or sets the headers.
        /// </summary>
        public IDictionary<string, byte[]> Headers { get; set; }

        /// <summary>
        /// Gets or sets the trailers.
        /// </summary>
        public IDictionary<string, byte[]> Trailers { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status info.
        /// </summary>
        public HttpStatusCode? HttpStatusCode { get; set; }

        /// <summary>
        /// Gets or sets the gRPC status info.
        /// </summary>
        public GrpcStatusInfo GrpcStatusInfo { get; set; }
    }
}
