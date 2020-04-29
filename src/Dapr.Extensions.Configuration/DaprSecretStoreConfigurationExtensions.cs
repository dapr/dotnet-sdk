// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using Dapr.Client;
using Microsoft.Extensions.Configuration;
using Dapr.Extensions.Configuration.DaprSecretStore;

namespace Dapr.Extensions.Configuration
{
    /// <summary>
    /// Extension methods for registering <see cref="DaprSecretStoreConfigurationProvider"/> with <see cref="IConfigurationBuilder"/>.
    /// </summary>
    public static class DaprSecretStoreConfigurationExtensions
    {
        /// <summary>
        /// Adds an <see cref="IConfigurationProvider"/> that reads configuration values from the Dapr Secret Store.
        /// </summary>
        /// <param name="configurationBuilder">The <see cref="IConfigurationBuilder"/> to add to.</param>
        /// <param name="store">Dapr secret store name.</param>
        /// <param name="secretDescriptors">The secrets to retrieve.</param>
        /// <param name="client">The Dapr client</param>
        /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
        public static IConfigurationBuilder AddDaprSecretStore(
            this IConfigurationBuilder configurationBuilder,
            string store,
            IEnumerable<DaprSecretDescriptor> secretDescriptors,
            Dapr.Client.DaprClient client)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(store, nameof(store));
            ArgumentVerifier.ThrowIfNull(secretDescriptors, nameof(secretDescriptors));
            ArgumentVerifier.ThrowIfNull(client, nameof(client));

            configurationBuilder.Add(new DaprSecretStoreConfigurationSource()
            {
                Store = store,
                SecretDescriptors = secretDescriptors,
                Client = client
            });

            return configurationBuilder;
        }

        /// <summary>
        /// Adds an <see cref="IConfigurationProvider"/> that reads configuration values from the command line.
        /// </summary>
        /// <param name="configurationBuilder">The <see cref="IConfigurationBuilder"/> to add to.</param>
        /// <param name="configureSource">Configures the source.</param>
        /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
        public static IConfigurationBuilder AddDaprSecretStore(this IConfigurationBuilder configurationBuilder, Action<DaprSecretStoreConfigurationSource> configureSource)
            => configurationBuilder.Add(configureSource);
    }
}
