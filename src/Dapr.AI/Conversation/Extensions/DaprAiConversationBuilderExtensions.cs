// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

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
    public static IDaprAiConversationBuilder AddDaprAiConversation(this IServiceCollection services, Action<IServiceProvider, DaprConversationClientBuilder>? configure = null, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        
        services.AddHttpClient();

        var registration = new Func<IServiceProvider, DaprConversationClient>(provider =>
        {
            var configuration = provider.GetService<IConfiguration>();
            var builder = new DaprConversationClientBuilder(configuration);

            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            builder.UseHttpClientFactory(httpClientFactory);

            configure?.Invoke(provider, builder);

            return builder.Build();
        });

        switch (lifetime)
        {
            case ServiceLifetime.Scoped:
                services.TryAddScoped(registration);
                break;
            case ServiceLifetime.Transient:
                services.TryAddTransient(registration);
                break;
            case ServiceLifetime.Singleton:
            default:
                services.TryAddSingleton(registration);
                break;
        }

        return new DaprAiConversationBuilder(services);
    }
}
