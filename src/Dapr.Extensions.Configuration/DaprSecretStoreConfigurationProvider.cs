// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapr.Client;
using Microsoft.Extensions.Configuration;

namespace Dapr.Extensions.Configuration.DaprSecretStore
{
    /// <summary>
    /// A Dapr Secret Store based <see cref="ConfigurationProvider"/>.
    /// </summary>
    internal class DaprSecretStoreConfigurationProvider : ConfigurationProvider
    {
        private readonly string store;

        private readonly IEnumerable<DaprSecretDescriptor> secretDescriptors;

        private readonly Dapr.Client.DaprClient client;

        /// <summary>
        /// Creates a new instance of <see cref="DaprSecretStoreConfigurationProvider"/>.
        /// </summary>
        /// <param name="store">Dapr Secre Store name.</param>
        /// <param name="secretDescriptors">The secrets to retrieve.</param>
        /// <param name="client">Dapr client used to retrieve Secrets</param>
        public DaprSecretStoreConfigurationProvider(string store, IEnumerable<DaprSecretDescriptor> secretDescriptors, Dapr.Client.DaprClient client)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(store, nameof(store));
            ArgumentVerifier.ThrowIfNull(secretDescriptors, nameof(secretDescriptors));
            ArgumentVerifier.ThrowIfNull(client, nameof(client));

            this.store = store;
            this.secretDescriptors = secretDescriptors;
            this.client = client;
        }

        public override void Load() => LoadAsync().ConfigureAwait(false).GetAwaiter().GetResult();

        private async Task LoadAsync()
        {
            var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var secretDescriptor in secretDescriptors)
            {
                var result = await client.GetSecretAsync(store, secretDescriptor.SecretName, secretDescriptor.Metadata).ConfigureAwait(false);

                foreach (var key in result.Keys)
                {
                    if (data.ContainsKey(key))
                    {
                        throw new InvalidOperationException($"A duplicate key '{key}' was found in the secret store '{store}'. Please remove any duplicates from your secret store.");
                    }

                    data.Add(key, result[key]);
                }
            }

            Data = data;
        }
    }
}
