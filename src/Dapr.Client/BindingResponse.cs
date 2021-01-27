// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Dapr.Client
{
    /// <summary>
    /// Represents the response from invoking a binding.
    /// </summary>
    public sealed class BindingResponse
    {
        /// <summary>
        /// Initializes a new <see cref="BindingResponse" />.`
        /// </summary>
        /// <param name="request">The <see cref="BindingRequest" /> assocated with this response.</param>
        /// <param name="data">The response payload.</param>
        /// <param name="metadata">The response metadata.</param>
        public BindingResponse(BindingRequest request, ReadOnlyMemory<byte> data, IReadOnlyDictionary<string, string> metadata)
        {
            ArgumentVerifier.ThrowIfNull(request, nameof(request));
            ArgumentVerifier.ThrowIfNull(data, nameof(data));
            ArgumentVerifier.ThrowIfNull(metadata, nameof(metadata));

            this.Request = request;
            this.Data = data;
            this.Metadata = metadata;
        }

        /// <summary>
        /// Gets the <see cref="BindingRequest" /> assocated with this response.
        /// </summary>
        public BindingRequest Request { get; }

        /// <summary>
        /// Gets the response payload.
        /// </summary>
        public ReadOnlyMemory<byte> Data { get; }

        /// <summary>
        /// Gets the response metadata.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; }
    }
}
