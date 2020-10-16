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
    /// This class is only needed if the app you are calling app is listening on gRPC.
    /// It contains propertes that represent status info that may be populated for an gRPC response.
    /// </summary>
    public class ServiceInvocationException<TRequest, TResponse> : Exception
    {
        /// <summary>
        /// The constructor.
        /// </summary>
        public ServiceInvocationException(ServiceInvocationResponse<TRequest, TResponse> response)
        {
            this.Response = response;
        }

        /// <summary>
        /// The constructor.
        /// </summary>
        public ServiceInvocationException(string message, ServiceInvocationResponse<TRequest, TResponse> response)
            : base(message)
        {
            this.Response = response;
        }

        /// <summary>
        /// The constructor.
        /// </summary>
        public ServiceInvocationException(string message, Exception innerException, ServiceInvocationResponse<TRequest, TResponse> response)
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
        public ServiceInvocationResponse<TRequest, TResponse> Response { get; set; }
    }
}
