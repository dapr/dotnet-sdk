using System.Text.Json.Serialization;
using Dapr.Common.JsonConverters;

namespace Dapr.AI.Conversation.ConversationRoles;

/// <summary>
/// The contents of a conversation message in the role of a user.
/// </summary>
public record UserMessage : IConversationMessage
{
    /// <summary>
    /// The role of the message.
    /// </summary>
    [JsonConverter(typeof(GenericEnumJsonConverter<MessageRole>))]
    [JsonPropertyName("role")]
    public MessageRole Role => MessageRole.User;

    /// <summary>
    /// The optional name of the user.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The contents of the message.
    /// </summary>
    public IReadOnlyList<MessageContent> Content { get; set; } = [];
}
