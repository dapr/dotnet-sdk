// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Net.Http;
using Grpc.Core;

namespace Dapr.Client
{
    /// <summary>
    /// The exception type thrown when an exception is encountered using Dapr service invocation.
    /// </summary>
    public class InvocationException : DaprException
    {
        /// <summary>
        /// Initializes a new <see cref="InvocationException" /> for a non-successful HTTP request.
        /// </summary>
        public InvocationException(string appId, string methodName, Exception innerException, HttpResponseMessage response)
            : base(FormatExceptionForFailedRequest(appId, methodName), innerException)
        {
            this.AppId = appId ?? "unknown";
            this.MethodName = methodName ?? "unknown";
            this.Response = response;
        }

        /// <summary>
        /// Initializes a new <see cref="InvocationException" /> for a non-successful gRPC request.
        /// </summary>
        public InvocationException(string appId, string methodName, RpcException innerException)
            : base(FormatExceptionForFailedRequest(appId, methodName), innerException)
        {
            this.AppId = appId ?? "unknown";
            this.MethodName = methodName ?? "unknown";
        }

        /// <summary>
        /// Gets the destination app-id of the invocation request that failed.
        /// </summary>
        public string AppId { get; }

        /// <summary>
        /// Gets the destination method name of the invocation request that failed.
        /// </summary>
        public string MethodName { get; }

        /// <summary>
        /// Gets the <see cref="HttpResponseMessage" /> of the request that failed. Will be <c>null</c> if the 
        /// failure was not related to an HTTP request or preventing the response from being recieved.
        /// </summary>
        public HttpResponseMessage Response { get; }

        private static string FormatExceptionForFailedRequest(string appId, string methodName)
        {
            return $"An exception occurred while invoking method: '{methodName}' on app-id: '{appId}'";
        }
    }
}
