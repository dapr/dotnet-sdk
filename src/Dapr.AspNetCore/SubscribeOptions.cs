// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    /// <summary>
    /// This class defines configurations for the subscribe endpoint.
    /// </summary>
    public class SubscribeOptions
    {
        /// <summary>
        /// Gets or Sets a value which indicates whether to enable or disable processing raw messages.
        /// </summary>
        public bool EnableRawPayload { get; set; }
    }
}
