// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    /// <summary>
    /// The REST API operations for Dapr runtime return standard HTTP status codes. This type defines the additional
    /// information returned from the Service Fabric API operations that are not successful.
    /// </summary>
    public class DaprError
    {
        /// <summary>
        /// Gets ErrorCode.
        /// </summary>        
        public string ErrorCode { get; set; }

        /// <summary>
        /// Gets error message.
        /// </summary>        
        public string Message { get; set; }
    }
}
