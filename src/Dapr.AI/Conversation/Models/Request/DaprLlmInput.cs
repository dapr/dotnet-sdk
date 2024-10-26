namespace Dapr.AI.Conversation.Models.Request;

/// <summary>
/// Represents an input for the Dapr Conversational API.
/// </summary>
/// <param name="Message">The message to send to the LLM.</param>
/// <param name="ScrubPII">If true, scrubs the data that goes into the LLM.</param>
/// <param name="Role">The role to set for the message.</param>
public sealed record DaprLlmInput(string Message, bool ScrubPII = false, string? Role = null);
