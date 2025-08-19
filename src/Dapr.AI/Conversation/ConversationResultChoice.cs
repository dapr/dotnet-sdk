using Dapr.AI.Conversation.Tools;

namespace Dapr.AI.Conversation;

/// <summary>
/// Represents a choice made by the model in the conversation.
/// </summary>
/// <param name="FinishReason">The reason why the model stopped generating tokens.</param>
/// <param name="Index">The index of the choice in the list of choices.</param>
/// <param name="Message">The message provided with the choice.</param>
public record ConversationResultChoice(FinishReason? FinishReason, long Index, ResultMessage Message);
