namespace Dapr.AI.Conversation.Tools;

/// <summary>
/// The main tool type to be used in a conversation.
/// </summary>
/// <param name="Name">The name of the function to be called.</param>
public record ToolFunction(string Name) : ITool
{
    /// <summary>
    /// A description of what the function does. The model uses this to choose when and how to
    /// call the function.
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// The parameters the function accepts.
    /// </summary>
    public IReadOnlyDictionary<string, object> Parameters { get; init; } = new Dictionary<string, object>();
}
