// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors
{
    using System;

    /// <summary>
    /// Represents connection settings for Http/gRPC Client to interact with Actions runtime.
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
