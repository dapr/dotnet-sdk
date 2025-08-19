namespace Dapr.AI.Conversation;

/// <summary>
/// Documents a tool call by a function within the context of a conversation message.
/// </summary>
/// <param name="Name">The name of the tool called.</param>
/// <param name="JsonArguments">The JSON arguments populated by the model. These might be hallucinated and invalid (e.g. format, values, etc.).</param>
public record CalledToolFunction(string Name, string JsonArguments) : ToolCallBase;
