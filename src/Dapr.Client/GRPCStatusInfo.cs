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
    public class GrpcStatusInfo
    {
        /// <summary>
        /// The constructor.
        /// </summary>
        public GrpcStatusInfo(Grpc.Core.StatusCode statusCode, string message)
        {
            this.GrpcStatusCode = statusCode;
            this.GrpcErrorMessage = message;
        }

        /// <summary>
        /// The gRPC Status Code
        /// </summary>
        public Grpc.Core.StatusCode GrpcStatusCode { get; }

        /// <summary>
        /// The gRPC Error Message
        /// </summary>
        public string GrpcErrorMessage { get; }
    }
}
