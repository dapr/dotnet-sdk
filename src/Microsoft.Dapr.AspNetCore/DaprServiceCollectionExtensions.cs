// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using System;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ApplicationModels;
    using Microsoft.Dapr;

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

            // StateHttpClient can be used with or without JsonSerializerOptions registered
            // in DI. If the user registers JsonSerializerOptions, it will be picked up by the client automatically.
            services.AddHttpClient("state").AddTypedClient<StateClient, StateHttpClient>();
        }

        private class DaprClientMarkerService
        {
        }
    }
}