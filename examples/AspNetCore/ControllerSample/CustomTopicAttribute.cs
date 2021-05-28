// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace ControllerSample
{
    using System;
    using Dapr;

    /// <summary>
    /// Sample custom <see cref="ITopicMetadata" /> implementation that returns topic metadata from environment variables.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class CustomTopicAttribute : Attribute, ITopicMetadata
    {
        public CustomTopicAttribute(string pubsubName, string name)
        {
            this.PubsubName = Environment.ExpandEnvironmentVariables(pubsubName);
            this.Name = Environment.ExpandEnvironmentVariables(name);
        }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public string PubsubName { get; }
    }
}
