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
