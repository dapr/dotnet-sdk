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

namespace Dapr
{
    /// <summary>
    /// Provides optional settings to the cloud events middleware.
    /// </summary>
    public class CloudEventsMiddlewareOptions
    {
        /// <summary>
        /// Gets or sets a value that will determine whether non-JSON textual payloads are decoded.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In the 1.0 release of the Dapr .NET SDK the cloud events middleware would not JSON-decode
        /// a textual cloud events payload. A cloud event payload containing <c>text/plain</c> data 
        /// of <c>"data": "Hello, \"world!\""</c> would result in a request body containing <c>"Hello, \"world!\""</c>
        /// instead of the expected JSON-decoded value of <c>Hello, "world!"</c>.
        /// </para>
        /// <para>
        /// Setting this property to <c>true</c> restores the previous invalid behavior for compatiblity.
        /// </para>
        /// </remarks>
        public bool SuppressJsonDecodingOfTextPayloads { get; set; }
    }
}
