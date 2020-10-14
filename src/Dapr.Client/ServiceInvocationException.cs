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
    public class ServiceInvocationException : Exception
    {
        /// <summary>
        /// The constructor.
        /// </summary>
        public ServiceInvocationException(InvokeRequest request, InvokeResponse response)
        {
            this.Request = request;
            this.Response = response;
        }

        /// <summary>
        /// The constructor.
        /// </summary>
        public ServiceInvocationException(string message, InvokeRequest request, InvokeResponse response)
            : base(message)
        {
            this.Request = request;
            this.Response = response;
        }

        /// <summary>
        /// The constructor.
        /// </summary>
        public ServiceInvocationException(string message, Exception innerException, InvokeRequest request, InvokeResponse response)
            : base(message, innerException)
        {
            this.Request = request;
            this.Response = response;
        }


        /// <summary>
        /// The gRPC Error Message
        /// </summary>
        public string GrpcErrorMessage { get; set; }

        /// <summary>
        /// The Request that caused the exception
        /// </summary>
        public InvokeRequest Request { get; set; }

        /// <summary>
        /// The Response
        /// </summary>
        public InvokeResponse Response { get; set; }
    }
}
