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
