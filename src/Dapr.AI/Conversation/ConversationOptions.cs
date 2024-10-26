// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

using Google.Protobuf.WellKnownTypes;

namespace Dapr.AI.Conversation;

/// <summary>
/// Options used to configure the conversation operation.
/// </summary>
/// <param name="ConversationId">The identifier of the conversation this is a continuation of.</param>
public sealed record ConversationOptions(string? ConversationId = null)
{
    /// <summary>
    /// Temperature for the LLM to optimize for creativity or predictability.
    /// </summary>
    public double Temperature { get; init; } = default;
    /// <summary>
    /// Flag that indicates whether data that comes back from the LLM should be scrubbed of PII data.
    /// </summary>
    public bool ScrubPII { get; init; } = default;
    /// <summary>
    /// The metadata passing to the conversation components.
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();
    /// <summary>
    /// Parameters for all custom fields.
    /// </summary>
    public Dictionary<string, Any> Parameters { get; init; } = new();
}
