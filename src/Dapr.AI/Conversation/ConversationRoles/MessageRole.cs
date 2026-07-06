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

using System.Runtime.Serialization;

namespace Dapr.AI.Conversation.ConversationRoles;

/// <summary>
/// Reflects the various roles assumed by a message within the context of a conversation.
/// </summary>
public enum MessageRole
{
    /// <summary>
    /// Reflects a developer role in a message.
    /// </summary>
    [EnumMember(Value="developer")]
    Developer,
    /// <summary>
    /// Reflects a system role in a message.
    /// </summary>
    [EnumMember(Value="system")]
    System,
    /// <summary>
    /// Reflects a user role in a message.
    /// </summary>
    [EnumMember(Value="user")]
    User,
    /// <summary>
    /// Reflects an assistant role in a message.
    /// </summary>
    [EnumMember(Value="assistant")]
    Assistant,
    /// <summary>
    /// Reflects a tool role in a message.
    /// </summary>
    [EnumMember(Value="tool")]
    Tool
}
