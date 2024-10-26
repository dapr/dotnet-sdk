using Google.Protobuf.WellKnownTypes;

namespace Dapr.AI.Conversation;

/// <summary>
/// Options used to configure the conversation operation.
/// </summary>
/// <param name="ConversationId">The identifier of the conversation this is a continuation of.</param>
public sealed record ConversationOptions(string? ConversationId = null)
{
    /// <summary>
    /// Temperature for the LLM to optimize for creativity or predictability.
    /// </summary>
    public double Temperature { get; init; } = default;
    /// <summary>
    /// Flag that indicates whether data that comes back from the LLM should be scrubbed of PII data.
    /// </summary>
    public bool ScrubPII { get; init; } = default;
    /// <summary>
    /// The metadata passing to the conversation components.
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();
    /// <summary>
    /// Parameters for all custom fields.
    /// </summary>
    public Dictionary<string, Any> Parameters { get; init; } = new();
}
