// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using System.Collections.Generic;
    using Dapr.Client;
    using Dapr.Client.Http;

    /// <summary>
    /// Represents an Invoke Response returned by service invocation
    /// </summary>
    public sealed class InvokeResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvokeResponse"/> class.
        /// </summary>
        /// <param name="request">The request.</param>
        public InvokeResponse(InvokeRequest request)
        {
            this.Request = request;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvokeResponse"/> class.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="body">The response body.</param>
        /// <param name="contentType">The content type.</param>
        /// <param name="httpStatusCode">HTTP status code (optional).</param>
        /// <param name="grpcStatusInfo">gRPC status info (optional).</param>
        public InvokeResponse(InvokeRequest request, byte[] body, string contentType, int? httpStatusCode = default, GRPCStatusInfo grpcStatusInfo = default)
        {
            this.Request = request;
            this.Body = body;
            this.ContentType = contentType;
            this.HttpStatusCode = httpStatusCode;
            this.GrpcStatusInfo = grpcStatusInfo;
        }

        /// <summary>
        /// Gets or sets the reference to Invoke Request.
        /// </summary>
        public InvokeRequest Request { get; set; }

        /// <summary>
        /// Gets or sets the response body.
        /// </summary>
        public byte[] Body { get; set; }

        /// <summary>
        /// Gets or sets the content type.
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Gets or sets the headers.
        /// </summary>
        public IDictionary<string, string> Headers { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status info.
        /// </summary>
        public int? HttpStatusCode { get; set; }

        /// <summary>
        /// Gets or sets the gRPC status info.
        /// </summary>
        public GRPCStatusInfo GrpcStatusInfo { get; set; }
    }
}
