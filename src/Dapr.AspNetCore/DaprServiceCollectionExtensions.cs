// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using System;
    using System.Linq;
    using Dapr.Client;

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
        /// <param name="configure"></param>
        public static void AddDaprClient(this IServiceCollection services, Action<DaprClientBuilder> configure = null)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            // This pattern prevents registering services multiple times in the case AddDaprClient is called
            // by non-user-code.
            if (services.Any(s => s.ImplementationType == typeof(DaprClientMarkerService)))
            {
                return;
            }

            services.AddSingleton<DaprClientMarkerService>();

            services.AddSingleton(_ =>
            {
                var builder = new DaprClientBuilder();
                if (configure != null)
                {
                    configure.Invoke(builder);
                }

                return builder.Build();
            });
        }

        private class DaprClientMarkerService
        {
        }
    }
}
