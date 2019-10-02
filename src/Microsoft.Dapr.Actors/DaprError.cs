// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Dapr.Actors
{
    /// <summary>
    /// The REST API operations for Dapr runtime return standard HTTP status codes. This type defines the additional
    /// information returned from the Service Fabric API operations that are not successful.
    /// </summary>
    public class DaprError
    {
        /// <summary>
        /// Initializes a new instance of the DaprError class.
        /// </summary>
        /// <param name="errorCode">Error code.</param>
        /// <param name="message">Error Message.</param>
        public DaprError(
            DaprErrorCodes? errorCode,
            string message = default)
        {
            errorCode.ThrowIfNull(nameof(errorCode));
            this.ErrorCode = errorCode;
            this.Message = message;
        }

        /// <summary>
        /// Gets ErrorCode.
        /// </summary>
        public DaprErrorCodes? ErrorCode { get; }

        /// <summary>
        /// Gets error message.
        /// </summary>
        public string Message { get; }
    }
}
