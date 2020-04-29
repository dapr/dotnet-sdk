// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    /// <summary>
    /// The HTTP verb to use for this message.
    /// </summary>
    public enum HTTPVerb
    {
        /// <summary>
        /// The HTTP verb POST
        /// </summary>
        Post,

        /// <summary>
        /// The HTTP verb GET
        /// </summary>
        Get,

        /// <summary>
        /// The HTTP verb PUT
        /// </summary>
        Put,

        /// <summary>
        /// The HTTP verb DELETE
        /// </summary>
        Delete
    }
}
