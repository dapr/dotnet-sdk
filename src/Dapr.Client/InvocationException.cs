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
    public class InvocationException : Exception
    {
        /// <summary>
        /// The constructor.
        /// </summary>
        public InvocationException()
        {
        }

        /// <summary>
        /// The constructor.
        /// </summary>
        public InvocationException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// The constructor.
        /// </summary>
        public InvocationException(string message, Exception innerException, GrpcStatusInfo grpcStatusInfo)
            : base(message, innerException)
        {
            this.GrpcStatusInfo = grpcStatusInfo;
        }

        /// <summary>
        /// The gRPC Status Info
        /// </summary>
        public GrpcStatusInfo GrpcStatusInfo { get; set; }
    }
}
