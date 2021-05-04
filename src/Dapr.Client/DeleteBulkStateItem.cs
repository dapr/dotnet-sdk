// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Collections.Generic;

namespace Dapr.Client
{
    /// <summary>
    /// Represents a state object used for bulk delete state operation
    /// </summary>
    public readonly struct BulkDeleteStateItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BulkStateItem"/> class.
        /// </summary>
        /// <param name="key">The state key.</param>
        /// <param name="etag">The ETag.</param>
        /// <param name="stateOptions">The stateOptions.</param>
        /// <param name="metadata">The metadata.</param>
        public BulkDeleteStateItem(string key, string etag, StateOptions stateOptions = default, IReadOnlyDictionary<string, string> metadata = default)
        {
            this.Key = key;
            this.ETag = etag;
            StateOptions = stateOptions;
            Metadata = metadata;
        }

         /// <summary>
        /// Gets the state key.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Get the ETag.
        /// </summary>
        public string ETag { get; }

        /// <summary>
        /// Gets the StateOptions.
        /// </summary>
        public StateOptions StateOptions { get; }

        /// <summary>
        /// Gets the Metadata.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; }
    }
}
