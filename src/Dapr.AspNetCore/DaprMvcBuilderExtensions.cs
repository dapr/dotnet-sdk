// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

namespace Microsoft.Extensions.DependencyInjection
{
    using System;
    using System.Linq;
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
        /// <param name="configureClient">The (optional) <see cref="DaprClientBuilder" /> to use for configuring the DaprClient.</param>
        /// <returns>The <see cref="IMvcBuilder" /> builder.</returns>
        public static IMvcBuilder AddDapr(this IMvcBuilder builder, Action<DaprClientBuilder> configureClient = null)
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            // This pattern prevents registering services multiple times in the case AddDapr is called
            // by non-user-code.
            if (builder.Services.Any(s => s.ImplementationType == typeof(DaprMvcMarkerService)))
            {
                return builder;
            }

            builder.Services.AddDaprClient(configureClient);

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
