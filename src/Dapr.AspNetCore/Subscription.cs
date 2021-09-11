// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Collections.Generic;

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
        /// Gets or sets the routes
        /// </summary>
        public Routes Routes { get; set; }

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
        /// Gets or sets the raw payload
        /// </summary>
        public string RawPayload { get; set; }
    }

    internal class Routes
    {
        /// <summary>
        /// Gets or sets the default route
        /// </summary>
        public string Default { get; set; }

        /// <summary>
        /// Gets or sets the routing rules
        /// </summary>
        public List<Rule> Rules { get; set; }
    }

    internal class Rule
    {
        /// <summary>
        /// Gets or sets the CEL expression to match this route.
        /// </summary>
        public string Match { get; set; }

        /// <summary>
        /// Gets or sets the path of the route.
        /// </summary>
        public string Path { get; set; }
    }
}
