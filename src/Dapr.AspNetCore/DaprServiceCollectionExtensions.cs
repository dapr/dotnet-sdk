// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using System;
    using Dapr;

    /// <summary>
    /// Provides extension methods for <see cref="IServiceCollection" />.
    /// </summary>
    public static class DaprServiceCollectionExtensions
    {
        /// <summary>
        /// Adds Dapr client services to the provided <see cref="IServiceCollection" />. This does not include integration
        /// with ASP.NET Core MVC. Use the <c>AddDapr()</c> extension method on <c>IMvcBuilder</c> to register MVC integration.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" />.</param>
        public static void AddDaprClient(this IServiceCollection services)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            // This pattern prevents registering services multiple times in the case AddDaprClient is called
            // by non-user-code.
            if (services.Contains(ServiceDescriptor.Singleton<DaprClientMarkerService, DaprClientMarkerService>()))
            {
                return;
            }

            services.AddSingleton<DaprClientMarkerService>();

            // StateHttpClient and InvokeHttpClient can be used with or without JsonSerializerOptions registered
            // in DI. If the user registers JsonSerializerOptions, it will be picked up by the client automatically.
            services.AddHttpClient("state").AddTypedClient<StateClient, StateHttpClient>();

            services.AddHttpClient("invoke").AddTypedClient<InvokeClient, InvokeHttpClient>();
        }

        private class DaprClientMarkerService
        {
        }
    }
}