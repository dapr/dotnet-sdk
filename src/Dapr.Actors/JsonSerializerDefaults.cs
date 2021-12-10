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

using System.Text.Json;

namespace Dapr.Actors
{
    /// <summary>
    /// A helper used to standardize the <see cref="JsonSerializerOptions"/> defaults
    /// </summary>
    internal static class JsonSerializerDefaults
    {
        /// <summary>
        /// <see cref="JsonSerializerOptions"/> defaults with Camel Casing and case insensitive properties
        /// </summary>
        internal static JsonSerializerOptions Web => new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }
}