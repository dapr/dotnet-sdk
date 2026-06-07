// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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

using System.Text.Json.Serialization;

namespace Dapr.Metadata.Abstractions;

/// <summary>
/// Represents the metadata for a registered Dapr actor.
/// </summary>
public class RegisteredActorMetadata
{
    /// <summary>
    /// The registered actor type.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }
    
    /// <summary>
    /// The number of actors running.
    /// </summary>
    [JsonPropertyName("count")]
    public int? Count { get; init; }
}
