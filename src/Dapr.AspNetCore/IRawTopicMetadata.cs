// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    /// <summary>
    /// RawMetadata that describes subscribe endpoint to enable or disable processing raw messages.
    /// </summary>
    public interface IRawTopicMetadata
    {
        /// <summary>
        /// Gets the enable or disable value for processing raw messages.
        /// </summary>
        bool? EnableRawPayload { get; }
    }
}
