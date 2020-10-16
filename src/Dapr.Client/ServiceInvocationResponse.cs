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
    /// <typeparam name="TRequest">Data type of the request.</typeparam>
    /// <typeparam name="TResponse">Data type of the response.</typeparam>
    public sealed class ServiceInvocationResponse<TRequest, TResponse>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceInvocationResponse{TRequest,TResponse}"/> class.
        /// </summary>
        public ServiceInvocationResponse()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceInvocationResponse{TRequest,TResponse}"/> class.
        /// </summary>
        /// <param name="request">The request.</param>
        public ServiceInvocationResponse(ServiceInvocationRequest<TRequest> request)
        {
            this.Request = request;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceInvocationResponse{TRequest,TResponse}"/> class.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="body">The response body.</param>
        /// <param name="headers">The response headers.</param>
        /// <param name="trailers">The response trailers.</param>
        /// <param name="contentType">The content type.</param>
        /// <param name="httpStatusCode">HTTP status code (optional).</param>
        /// <param name="grpcStatusInfo">gRPC status info (optional).</param>
        public ServiceInvocationResponse(
            ServiceInvocationRequest<TRequest> request,
            TResponse body, string contentType,
            IDictionary<string, byte[]> headers,
            IDictionary<string, byte[]> trailers,
            HttpStatusCode? httpStatusCode = default,
            GRPCStatusInfo grpcStatusInfo = default)
        {
            this.Request = request;
            this.Body = body;
            this.Headers = headers;
            this.Trailers = trailers;
            this.ContentType = contentType;
            this.HttpStatusCode = httpStatusCode;
            this.GrpcStatusInfo = grpcStatusInfo;
        }

        /// <summary>
        /// Gets or sets the reference to Invoke Request.
        /// </summary>
        public ServiceInvocationRequest<TRequest> Request { get; set; }

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
        public GRPCStatusInfo GrpcStatusInfo { get; set; }
    }
}
