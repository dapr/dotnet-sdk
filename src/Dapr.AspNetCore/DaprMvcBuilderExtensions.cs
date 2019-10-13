// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using System;
    using Dapr;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ApplicationModels;

    /// <summary>
    /// Provides extension methods for <see cref="IMvcBuilder" />.
    /// </summary>
    public static class DaprMvcBuilderExtensions
    {
        /// <summary>
        /// Adds Dapr integration for MVC to the provided <see cref="IMvcBuilder" />.
        /// </summary>
        /// <param name="builder">The <see cref="IMvcBuilder" />.</param>
        public static void AddDapr(this IMvcBuilder builder)
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            // This pattern prevents registering services multiple times in the case AddDapr is called
            // by non-user-code.
            if (builder.Services.Contains(ServiceDescriptor.Singleton<DaprMvcMarkerService, DaprMvcMarkerService>()))
            {
                return;
            }

            builder.Services.AddDaprClient();

            builder.Services.AddSingleton<DaprMvcMarkerService>();
            builder.Services.AddSingleton<IApplicationModelProvider, StateEntryApplicationModelProvider>();
            builder.Services.Configure<MvcOptions>(options =>
            {
                options.ModelBinderProviders.Insert(0, new StateEntryModelBinderProvider());
            });
        }

        private class DaprMvcMarkerService
        {
        }
    }
}