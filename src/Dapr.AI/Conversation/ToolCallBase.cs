namespace Dapr.AI.Conversation;

/// <summary>
/// Represents a call to a tool within the context of a conversation.
/// </summary>
public abstract record ToolCallBase
{
    /// <summary>
    /// An optional tool identifier.
    /// </summary>
    public string? Id { get; set; }
}
