namespace Dapr.AI.Conversation.Models.Response;

/// <summary>
/// The response for a conversation.
/// </summary>
/// <param name="Outputs">The collection of conversation results.</param>
public record DaprConversationResponse(List<DaprConversationResult> Outputs)
{
    /// <summary>
    /// The identifier of an existing or newly created conversation.
    /// </summary>
    public string? ConversationId { get; init; }
}
