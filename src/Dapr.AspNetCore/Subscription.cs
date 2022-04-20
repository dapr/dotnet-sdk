// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

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
