// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client.Http
{
    /// <summary>
    /// The HTTP verb to use for this message.
    /// </summary>
    public enum HTTPVerb
    {
        /// <summary>
        /// The HTTP verb GET
        /// </summary>
        Get,

        /// <summary>
        /// The HTTP verb HEAD
        /// </summary>
        Head,

        /// <summary>
        /// The HTTP verb POST
        /// </summary>
        Post,      

        /// <summary>
        /// The HTTP verb PUT
        /// </summary>
        Put,

        /// <summary>
        /// The HTTP verb DELETE
        /// </summary>
        Delete,

        /// <summary>
        /// The HTTP verb CONNECT
        /// </summary>
        Connect,

        /// <summary>
        /// The HTTP verb OPTIONS
        /// </summary>
        Options,

        /// <summary>
        /// The HTTP verb TRACE
        /// </summary>
        Trace
    }
}
