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
        /// Adds Dapr services to the provided <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" />.</param>
        public static void AddDapr(this IServiceCollection services)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            // This pattern prevents registering services multiple times in the case AddDapr is called
            // by non-user-code.
            if (services.Contains(ServiceDescriptor.Singleton<DaprMarkerService, DaprMarkerService>()))
            {
                return;
            }

            services.AddSingleton<DaprMarkerService>();
            services.AddSingleton<IApplicationModelProvider, StateEntryApplicationModelProvider>();
            services.Configure<MvcOptions>(options =>
            {
                options.ModelBinderProviders.Insert(0, new StateEntryModelBinderProvider());
            });
        }

        private class DaprMarkerService
        {
        }
    }
}