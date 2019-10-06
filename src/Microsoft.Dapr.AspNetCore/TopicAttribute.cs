// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Dapr
{
    using System;
    
    /// <summary>
    /// Metadata that describes an endpoint as a subscriber to a topic.
    /// </summary>
    public class TopicAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of <see cref="TopicAttribute" />.
        /// </summary>
        /// <param name="name">The topic name.</param>
        public TopicAttribute(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("The value cannot be null or empty.", nameof(name));
            }

            this.Name = name;
        }

        /// <summary>
        /// Gets the topic name.
        /// </summary>
        public string Name { get; }
    }
}