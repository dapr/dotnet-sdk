// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    /// <summary>
    /// Represents a state object returned from a bulk get state operation.
    /// </summary>
    public readonly struct GetBulkItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GetBulkItem"/> class.
        /// </summary>
        /// <param name="key">The state key.</param>
        /// <param name="value">The value.</param>
        /// <param name="etag">The ETag.</param>
        /// <remarks>
        /// Application code should not need to create instances of <see cref="GetBulkItem" />.
        /// </remarks>
        public GetBulkItem(string key, string value, string etag)
        {
            this.Key = key;
            this.Value = value;
            this.ETag = etag;
        }

         /// <summary>
        /// Gets the state key.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets the value.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Get the ETag.
        /// </summary>
        public string ETag { get; }
    }
}