using Microsoft.Extensions.DependencyInjection;

namespace Dapr.AI.Conversation.Extensions;

/// <summary>
/// Used by the fluent registration builder to configure a Dapr AI conversational manager.
/// </summary>
public sealed class DaprAiConversationBuilder : IDaprAiConversationBuilder
{
    /// <summary>
    /// The registered services on the builder.
    /// </summary>
    public IServiceCollection Services { get;  }

    /// <summary>
    /// Used to initialize a new <see cref="DaprAiConversationBuilder"/>.
    /// </summary>
    public DaprAiConversationBuilder(IServiceCollection services)
    {
        Services = services;
    }
}
