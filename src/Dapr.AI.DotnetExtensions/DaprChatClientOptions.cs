namespace Dapr.AI.DotnetExtensions;

/// <summary>
/// Provides the required operation for configuring the <see cref="DaprChatClient"/>.
/// </summary>
public sealed class DaprChatClientOptions
{
    /// <summary>
    /// The name of the Dapr Conversation component to use. 
    /// </summary>
    public required string ConversationComponentName { get; set; }
}
