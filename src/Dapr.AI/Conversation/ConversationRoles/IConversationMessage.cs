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

namespace Dapr.AI.Conversation.ConversationRoles;

/// <summary>
/// A base interface for a role-based message.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "Role")]
[JsonDerivedType(typeof(AssistantMessage), "assistant")]
[JsonDerivedType(typeof(DeveloperMessage), "developer")]
[JsonDerivedType(typeof(SystemMessage), "system")]
[JsonDerivedType(typeof(ToolMessage), "tool")]
[JsonDerivedType(typeof(UserMessage), "user")]
public interface IConversationMessage
{
    /// <summary>
    /// The role of the message.
    /// </summary>
    MessageRole Role { get; }
    
    /// <summary>
    /// The content of the message.
    /// </summary>
    IReadOnlyList<MessageContent> Content { get; set; }
}
