// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------
namespace Dapr.Client
{
    using Dapr.Client.Http;
    using System;
    using System.Net;

    /// <summary>
    /// This class represents the exception thrown when Service Invocation via Dapr encounters an error
    /// </summary>
    public class ServiceInvocationException<TRequest, TResponse> : Exception
    {
        /// <summary>
        /// The constructor.
        /// </summary>
        public ServiceInvocationException(InvocationResponse<TRequest, TResponse> response)
        {
            this.Response = response;
        }

        /// <summary>
        /// The constructor.
        /// </summary>
        public ServiceInvocationException(string message, InvocationResponse<TRequest, TResponse> response)
            : base(message)
        {
            this.Response = response;
        }

        /// <summary>
        /// The constructor.
        /// </summary>
        public ServiceInvocationException(string message, Exception innerException, InvocationResponse<TRequest, TResponse> response)
            : base(message, innerException)
        {
            this.Response = response;
        }


        /// <summary>
        /// The gRPC Error Message
        /// </summary>
        public string GrpcErrorMessage { get; set; }

        /// <summary>
        /// The Response
        /// </summary>
        public InvocationResponse<TRequest, TResponse> Response { get; set; }
    }
}
