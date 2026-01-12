namespace Dapr.AI.Conversation;

/// <summary>
/// The result in a conversation for a given input.
/// </summary>
/// <param name="Choices">The resulting choices from the conversation.</param>
public sealed record ConversationResponseResult(IReadOnlyList<ConversationResultChoice> Choices);
