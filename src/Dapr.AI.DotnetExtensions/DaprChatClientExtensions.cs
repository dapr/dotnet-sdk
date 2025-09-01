using System.Diagnostics.CodeAnalysis;
using Dapr.AI.Conversation;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Dapr.AI.DotnetExtensions;

/// <summary>
/// Contains extension methods for an <see cref="IServiceCollection"/> for registering <see cref="DaprChatClient"/>.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="DaprChatClient"/> requires <see cref="DaprConversationClient"/> to be registered in the DI container.
/// Make sure to call <c>AddDaprConversationClient()</c> before calling any of these extension methods.
/// </para>
/// <example>
/// <code>
/// services.AddDaprConversationClient();
/// services.AddDaprChatClient("conversation");
/// </code>
/// </example>
/// </remarks>
[Experimental("DAPR_CONVERSATION", UrlFormat = "https://docs.dapr.io/developing-applications/building-blocks/conversation/conversation-overview/")]
public static class DaprChatClientExtensions
{
    /// <summary>
    /// Registers <see cref="DaprChatClient"/> as a service that implements <see cref="IChatClient"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="conversationComponentName">The name of the Dapr Conversation component.</param>
    /// <param name="serviceLifetime">The <see cref="ServiceLifetime"/> of the service. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown at runtime if <see cref="DaprConversationClient"/> is not registered in the DI container.
    /// </exception>
    public static IServiceCollection AddDaprChatClient(this IServiceCollection services, string conversationComponentName, ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationComponentName, nameof(conversationComponentName));

        return services.AddDaprChatClient(options =>
        {
            options.ConversationComponentName = conversationComponentName;
        }, serviceLifetime);
    }

    /// <summary>
    /// Registers <see cref="DaprChatClient"/> as a service that implements <see cref="IChatClient"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="conversationComponentName">The name of the Dapr Conversation component.</param>
    /// <param name="configure">An optional <see cref="Action{T}"/> to configure the <see cref="DaprChatClientOptions"/>.</param>
    /// <param name="serviceLifetime">The <see cref="ServiceLifetime"/> of the service. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown at runtime if <see cref="DaprConversationClient"/> is not registered in the DI container.
    /// </exception>
    public static IServiceCollection AddDaprChatClient(this IServiceCollection services, string conversationComponentName,
        Action<DaprChatClientOptions>? configure = null, ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationComponentName);

        return services.AddDaprChatClient(options =>
        {
            options.ConversationComponentName = conversationComponentName;
            configure?.Invoke(options);
        }, serviceLifetime);
    }

    /// <summary>
    /// Registers <see cref="DaprChatClient"/> as a service that implements <see cref="IChatClient"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configure">An <see cref="Action{T}"/> to configure the <see cref="DaprChatClientOptions"/>.</param>
    /// <param name="serviceLifetime">The <see cref="ServiceLifetime"/> of the service. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown at runtime if <see cref="DaprConversationClient"/> is not registered in the DI container.
    /// </exception>
    public static IServiceCollection AddDaprChatClient(this IServiceCollection services,
        Action<DaprChatClientOptions> configure, ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        
        services.TryAdd(ServiceDescriptor.Describe(typeof(IChatClient), serviceProvider =>
        {
            var daprConversationClient = serviceProvider.GetRequiredService<DaprConversationClient>();
            var options = serviceProvider.GetRequiredService<IOptions<DaprChatClientOptions>>();
            return new DaprChatClient(daprConversationClient, serviceProvider, options);
        }, serviceLifetime));

        return services;
    }
}
