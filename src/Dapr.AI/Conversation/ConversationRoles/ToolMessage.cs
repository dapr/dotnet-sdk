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
