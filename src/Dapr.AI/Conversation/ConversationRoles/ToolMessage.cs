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
using Dapr.Common.JsonConverters;

namespace Dapr.AI.Conversation.ConversationRoles;

/// <summary>
/// The contents of a conversation message in the role of a tool.
/// </summary>
public record ToolMessage : IConversationMessage
{
    /// <summary>
    /// The role of the message.
    /// </summary>
    [JsonConverter(typeof(GenericEnumJsonConverter<MessageRole>))]
    [JsonPropertyName("role")]
    public MessageRole Role => MessageRole.Tool;
    
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// The identifier of the tool.
    /// </summary>
    public string? Id { get; set; }
    
    /// <summary>
    /// The content of the message.
    /// </summary>
    public IReadOnlyList<MessageContent> Content { get; set; } = [];
}
