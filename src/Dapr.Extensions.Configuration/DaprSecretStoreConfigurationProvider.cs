// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
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

        private readonly bool normalizeKey;

        private readonly IList<string>? keyDelimiters;

        private readonly IEnumerable<DaprSecretDescriptor>? secretDescriptors;

        private readonly IReadOnlyDictionary<string, string>? metadata;

        private readonly DaprClient client;

        /// <summary>
        /// Creates a new instance of <see cref="DaprSecretStoreConfigurationProvider"/>.
        /// </summary>
        /// <param name="store">Dapr Secre Store name.</param>
        /// <param name="normalizeKey">Indicates whether any key delimiters should be replaced with the delimiter ":".</param>
        /// <param name="secretDescriptors">The secrets to retrieve.</param>
        /// <param name="client">Dapr client used to retrieve Secrets</param>
        public DaprSecretStoreConfigurationProvider(string store, bool normalizeKey, IEnumerable<DaprSecretDescriptor> secretDescriptors, DaprClient client)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(store, nameof(store));
            ArgumentVerifier.ThrowIfNull(secretDescriptors, nameof(secretDescriptors));
            ArgumentVerifier.ThrowIfNull(client, nameof(client));

            if (secretDescriptors.Count() == 0)
            {
                throw new ArgumentException("No secret descriptor was provided", nameof(secretDescriptors));
            }

            this.store = store;
            this.normalizeKey = normalizeKey;
            this.secretDescriptors = secretDescriptors;
            this.client = client;
        }

        /// <summary>
        /// Creates a new instance of <see cref="DaprSecretStoreConfigurationProvider"/>.
        /// </summary>
        /// <param name="store">Dapr Secre Store name.</param>
        /// <param name="normalizeKey">Indicates whether any key delimiters should be replaced with the delimiter ":".</param>
        /// <param name="keyDelimiters">A collection of delimiters that will be replaced by ':' in the key of every secret.</param>
        /// <param name="secretDescriptors">The secrets to retrieve.</param>
        /// <param name="client">Dapr client used to retrieve Secrets</param>
        public DaprSecretStoreConfigurationProvider(string store, bool normalizeKey, IList<string>? keyDelimiters, IEnumerable<DaprSecretDescriptor> secretDescriptors, DaprClient client)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(store, nameof(store));
            ArgumentVerifier.ThrowIfNull(secretDescriptors, nameof(secretDescriptors));
            ArgumentVerifier.ThrowIfNull(client, nameof(client));

            if (secretDescriptors.Count() == 0)
            {
                throw new ArgumentException("No secret descriptor was provided", nameof(secretDescriptors));
            }

            this.store = store;
            this.normalizeKey = normalizeKey;
            this.keyDelimiters = keyDelimiters;
            this.secretDescriptors = secretDescriptors;
            this.client = client;
        }

        /// <summary>
        /// Creates a new instance of <see cref="DaprSecretStoreConfigurationProvider"/>.
        /// </summary>
        /// <param name="store">Dapr Secre Store name.</param>
        /// <param name="normalizeKey">Indicates whether any key delimiters should be replaced with the delimiter ":".</param>
        /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the secret store. The valid metadata keys and values are determined by the type of secret store used.</param>
        /// <param name="client">Dapr client used to retrieve Secrets</param>
        public DaprSecretStoreConfigurationProvider(string store, bool normalizeKey, IReadOnlyDictionary<string, string>? metadata, DaprClient client)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(store, nameof(store));
            ArgumentVerifier.ThrowIfNull(client, nameof(client));

            this.store = store;
            this.normalizeKey = normalizeKey;
            this.metadata = metadata;
            this.client = client;
        }

        /// <summary>
        /// Creates a new instance of <see cref="DaprSecretStoreConfigurationProvider"/>.
        /// </summary>
        /// <param name="store">Dapr Secre Store name.</param>
        /// <param name="normalizeKey">Indicates whether any key delimiters should be replaced with the delimiter ":".</param>
        /// <param name="keyDelimiters">A collection of delimiters that will be replaced by ':' in the key of every secret.</param>
        /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the secret store. The valid metadata keys and values are determined by the type of secret store used.</param>
        /// <param name="client">Dapr client used to retrieve Secrets</param>
        public DaprSecretStoreConfigurationProvider(string store, bool normalizeKey, IList<string>? keyDelimiters, IReadOnlyDictionary<string, string>? metadata, DaprClient client)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(store, nameof(store));
            ArgumentVerifier.ThrowIfNull(client, nameof(client));

            this.store = store;
            this.normalizeKey = normalizeKey;
            this.keyDelimiters = keyDelimiters;
            this.metadata = metadata;
            this.client = client;
        }

        private string NormalizeKey(string key)
        {
            if (this.keyDelimiters?.Count > 0)
            {
                foreach (var keyDelimiter in this.keyDelimiters)
                {
                    key = key.Replace(keyDelimiter, ConfigurationPath.KeyDelimiter);
                }
            }

            return key;
        }

        public override void Load() => LoadAsync().ConfigureAwait(false).GetAwaiter().GetResult();

        private async Task LoadAsync()
        {
            var data = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            if (secretDescriptors != null)
            {
                foreach (var secretDescriptor in secretDescriptors)
                {
                    var result = await client.GetSecretAsync(store, secretDescriptor.SecretName, secretDescriptor.Metadata).ConfigureAwait(false);

                    foreach (var key in result.Keys)
                    {
                        if (data.ContainsKey(key))
                        {
                            throw new InvalidOperationException($"A duplicate key '{key}' was found in the secret store '{store}'. Please remove any duplicates from your secret store.");
                        }

                        data.Add(normalizeKey ? NormalizeKey(key) : key, result[key]);
                    }
                }

                Data = data;
            }
            else
            {
                var result = await client.GetBulkSecretAsync(store, metadata).ConfigureAwait(false);
                foreach (var key in result.Keys)
                {
                    foreach (var secret in result[key])
                    {
                        if (data.ContainsKey(secret.Key))
                        {
                            throw new InvalidOperationException($"A duplicate key '{secret.Key}' was found in the secret store '{store}'. Please remove any duplicates from your secret store.");
                        }

                        data.Add(normalizeKey ? NormalizeKey(secret.Key) : secret.Key, secret.Value);
                    }
                }
                Data = data;
            }
        }
    }
}
