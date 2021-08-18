// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    /// <summary>
    /// This class defines subscribe endpoint response
    /// </summary>
    internal class Subscription
    {
        /// <summary>
        /// Gets or sets the topic name.
        /// </summary>
        public string Topic { get; set; }

        /// <summary>
        /// Gets or sets the pubsub name
        /// </summary>
        public string PubsubName { get; set; }

        /// <summary>
        /// Gets or sets the route
        /// </summary>
        public string Route { get; set; }

        /// <summary>
        /// Gets or sets the metadata.
        /// </summary>
        public Metadata Metadata { get; set; }
    }

    /// <summary>
    /// This class defines the metadata for subscribe endpoint.
    /// </summary>
    internal class Metadata
    {
        /// <summary>
        /// Gets or sets the rawoayload
        /// </summary>
        public string RawPayload { get; set; }
    }
}
