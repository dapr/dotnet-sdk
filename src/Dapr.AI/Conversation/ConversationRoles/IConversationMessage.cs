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
