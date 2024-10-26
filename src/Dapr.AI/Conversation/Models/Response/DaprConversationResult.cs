using Google.Protobuf.WellKnownTypes;

namespace Dapr.AI.Conversation.Models.Response;

/// <summary>
/// The result for a single conversational input.
/// </summary>
/// <param name="Result">The result for one conversation input.</param>
public record DaprConversationResult(string Result)
{
    /// <summary>
    /// Parameters for all custom fields.
    /// </summary>
    public Dictionary<string, Any> Parameters { get; init; } = new();
}
