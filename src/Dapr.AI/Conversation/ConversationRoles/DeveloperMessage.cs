using System.Text.Json.Serialization;
using Dapr.Common.JsonConverters;

namespace Dapr.AI.Conversation.ConversationRoles;

/// <summary>
/// The contents of a conversation message in the role of a developer.
/// </summary>
public record DeveloperMessage : IConversationMessage
{
    /// <summary>
    /// The role of the message.
    /// </summary>
    [JsonConverter(typeof(GenericEnumJsonConverter<MessageRole>))]
    [JsonPropertyName("role")]
    public MessageRole Role => MessageRole.Developer;
    
    /// <summary>
    /// The name of the participant in the message.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The content of the message.
    /// </summary>
    public IReadOnlyList<MessageContent> Content { get; set; } = [];
}
