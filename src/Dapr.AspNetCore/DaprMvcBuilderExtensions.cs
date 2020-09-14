// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using System;
    using System.Text.Json;
    using Dapr;
    using Dapr.AspNetCore;
    using Dapr.Client;
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
        /// <param name="configure">The (optional) <see cref="DaprClientBuilder" /> to use for building the DaprClient.</param>
        /// <returns>The <see cref="IMvcBuilder" /> builder.</returns>
        public static IMvcBuilder AddDapr(this IMvcBuilder builder, Action<DaprClientBuilder> configure = null)
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            // This pattern prevents registering services multiple times in the case AddDapr is called
            // by non-user-code.
            if (builder.Services.Contains(ServiceDescriptor.Singleton<DaprMvcMarkerService, DaprMvcMarkerService>()))
            {
                return builder;
            }

            // create default clientbuilder config (if not specified)
            if (configure == null)
            {
                configure = new Action<DaprClientBuilder>(
                    builder => builder.UseJsonSerializationOptions(
                        new JsonSerializerOptions()
                        {
                            PropertyNameCaseInsensitive = true
                        }));
            }

            builder.Services.AddDaprClient(configure);

            builder.Services.AddSingleton<DaprMvcMarkerService>();
            builder.Services.AddSingleton<IApplicationModelProvider, StateEntryApplicationModelProvider>();
            builder.Services.Configure<MvcOptions>(options =>
            {
                options.ModelBinderProviders.Insert(0, new StateEntryModelBinderProvider());
            });

            return builder;
        }

        private class DaprMvcMarkerService
        {
        }
    }
}
