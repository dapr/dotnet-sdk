using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dapr.AI.Conversation.Extensions;

/// <summary>
/// Contains the dependency injection registration extensions for the Dapr AI Conversation operations.
/// </summary>
public static class DaprAiConversationBuilderExtensions
{
    /// <summary>
    /// Registers the necessary functionality for the Dapr AI conversation functionality.
    /// </summary>
    /// <returns></returns>
    public static IDaprAiConversationBuilder AddDaprAiConversation(this IServiceCollection services, Action<IServiceProvider, DaprConversationClientBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        
        services.AddHttpClient();
        
        services.TryAddSingleton(provider =>
        {
            var configuration = provider.GetService<IConfiguration>();
            var builder = new DaprConversationClientBuilder(configuration);
            
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            builder.UseHttpClientFactory(httpClientFactory);

            configure?.Invoke(provider, builder);

            return builder.Build();
        });

        return new DaprAiConversationBuilder(services);
    }
}
