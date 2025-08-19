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

using Dapr.AI.Conversation.Tools;
using Google.Protobuf.WellKnownTypes;

namespace Dapr.AI.Conversation;

/// <summary>
/// Options used to configure the conversation operation.
/// </summary>
/// <param name="ConversationComponentId">The ID of the conversation component to use.</param>
public sealed record ConversationOptions(string ConversationComponentId)
{
    /// <summary>
    /// The ID of an existing chat.
    /// </summary>
    public string? ContextId { get; init; }
    
    /// <summary>
    /// Temperature for the LLM to optimize for creativity or predictability.
    /// </summary>
    public double? Temperature { get; init; }
    
    /// <summary>
    /// Flag that indicates whether data that comes back from the LLM should be scrubbed of PII data.
    /// </summary>
    public bool? ScrubPII { get; init; }
    
    /// <summary>
    /// The metadata passing to the conversation components.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
    
    /// <summary>
    /// Parameters for all custom fields.
    /// </summary>
    public IReadOnlyDictionary<string, Any> Parameters { get; init; } = new Dictionary<string, Any>();

    /// <summary>
    /// Registers the tools available to be used by the LLM during the conversation. These are sent on a per-request
    /// basis.
    /// </summary>
    /// <remarks>
    /// The tools available during the first round of the conversation may be different than the tools specified later
    /// on.
    /// </remarks>
    public IReadOnlyList<ITool> Tools { get; init; } = [];

    /// <summary>
    /// Controls which (if any) tool is called by the model.
    /// - 'none' means that the model will not call any tool and instead generate a message.
    /// - 'auto' means that the model can picked between generating a message or calling one or more tools.
    /// - Alternatively, a specific tool name may be used here and casing/syntax must match on the tool name.
    /// </summary>
    /// <remarks>
    /// - 'none' is the default when no tools are present.
    /// - 'auto' is the default if tools are present.
    /// </remarks>
    public ToolChoice? ToolChoice { get; init; }
}
