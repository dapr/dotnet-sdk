// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors
{
    using System;

    /// <summary>
    /// Represents connection settings for Http/gRPC Client to interact with Dapr runtime.
    /// </summary>
    internal class ClientSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClientSettings"/> class.
        /// </summary>
        /// <param name="clientTimeout">Timespan to wait before the request times out for the client.</param>
        public ClientSettings(TimeSpan? clientTimeout = null)
        {
            this.ClientTimeout = clientTimeout;
        }

        /// <summary>
        /// Gets or sets the Timespan to wait before the request times out for the client.
        /// </summary>
        public TimeSpan? ClientTimeout { get; set; }
    }
}
