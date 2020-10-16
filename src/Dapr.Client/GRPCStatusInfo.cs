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
    public class GRPCStatusInfo
    {
        /// <summary>
        /// The constructor.
        /// </summary>
        public GRPCStatusInfo(Grpc.Core.StatusCode statusCode, string message, int? innerHttpStatusCode = default, string InnerHttpErrorMessage = default)
        {
            this.GrpcStatusCode = statusCode;
            this.GrpcErrorMessage = message;
            this.InnerHttpStatusCode = innerHttpStatusCode;
            this.InnerHttpErrorMessage = InnerHttpErrorMessage;
        }

        /// <summary>
        /// The gRPC Status Code
        /// </summary>
        public Grpc.Core.StatusCode GrpcStatusCode { get; set; }

        /// <summary>
        /// The gRPC Error Message
        /// </summary>
        public string GrpcErrorMessage { get; set; }

        /// <summary>
        /// The HTTP Status Code
        /// </summary>
        public int? InnerHttpStatusCode { get; set; }

        /// <summary>
        /// The HTTP Error Message
        /// </summary>
        public string InnerHttpErrorMessage { get; set; }
    }
}
