// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------
namespace Dapr.Client
{
    using System;

    /// <summary>
    /// This class represents the exception thrown when Service Invocation via Dapr encounters an error
    /// </summary>
    public class InvocationException : Exception
    {
        /// <summary>
        /// The constructor.
        /// </summary>
        public InvocationException(string message, Exception innerException, InvocationResponse<byte[]> response)
            : base(message, innerException)
        {
            this.Response = response;
        }

        /// <summary>
        /// The Response
        /// </summary>
        public InvocationResponse<byte[]> Response { get; }
    }
}
