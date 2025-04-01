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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;
using Microsoft.Extensions.Configuration;

namespace Dapr.Extensions.Configuration.DaprSecretStore;

/// <summary>
/// A Dapr Secret Store based <see cref="ConfigurationProvider"/>.
/// </summary>
internal class DaprSecretStoreConfigurationProvider : ConfigurationProvider
{
    internal static readonly TimeSpan DefaultSidecarWaitTimeout = TimeSpan.FromSeconds(5);

    private readonly string store;

    private readonly bool normalizeKey;

    private readonly IList<string>? keyDelimiters;

    private readonly IEnumerable<DaprSecretDescriptor>? secretDescriptors;

    private readonly IReadOnlyDictionary<string, string>? metadata;

    private readonly DaprClient client;

    private readonly TimeSpan sidecarWaitTimeout;

    /// <summary>
    /// Creates a new instance of <see cref="DaprSecretStoreConfigurationProvider"/>.
    /// </summary>
    /// <param name="store">Dapr Secret Store name.</param>
    /// <param name="normalizeKey">Indicates whether any key delimiters should be replaced with the delimiter ":".</param>
    /// <param name="secretDescriptors">The secrets to retrieve.</param>
    /// <param name="client">Dapr client used to retrieve Secrets</param>
    public DaprSecretStoreConfigurationProvider(
        string store,
        bool normalizeKey,
        IEnumerable<DaprSecretDescriptor> secretDescriptors,
        DaprClient client) : this(store, normalizeKey, null, secretDescriptors, client, DefaultSidecarWaitTimeout)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="DaprSecretStoreConfigurationProvider"/>.
    /// </summary>
    /// <param name="store">Dapr Secret Store name.</param>
    /// <param name="normalizeKey">Indicates whether any key delimiters should be replaced with the delimiter ":".</param>
    /// <param name="keyDelimiters">A collection of delimiters that will be replaced by ':' in the key of every secret.</param>
    /// <param name="secretDescriptors">The secrets to retrieve.</param>
    /// <param name="client">Dapr client used to retrieve Secrets</param>
    public DaprSecretStoreConfigurationProvider(
        string store,
        bool normalizeKey,
        IList<string>? keyDelimiters,
        IEnumerable<DaprSecretDescriptor> secretDescriptors,
        DaprClient client) : this(store, normalizeKey, keyDelimiters, secretDescriptors, client, DefaultSidecarWaitTimeout)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="DaprSecretStoreConfigurationProvider"/>.
    /// </summary>
    /// <param name="store">Dapr Secret Store name.</param>
    /// <param name="normalizeKey">Indicates whether any key delimiters should be replaced with the delimiter ":".</param>
    /// <param name="keyDelimiters">A collection of delimiters that will be replaced by ':' in the key of every secret.</param>
    /// <param name="secretDescriptors">The secrets to retrieve.</param>
    /// <param name="client">Dapr client used to retrieve Secrets</param>
    /// <param name="sidecarWaitTimeout">The <see cref="TimeSpan"/> used to configure the timeout waiting for Dapr.</param>
    public DaprSecretStoreConfigurationProvider(
        string store,
        bool normalizeKey,
        IList<string>? keyDelimiters,
        IEnumerable<DaprSecretDescriptor> secretDescriptors,
        DaprClient client,
        TimeSpan sidecarWaitTimeout)
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
        this.sidecarWaitTimeout = sidecarWaitTimeout;
    }

    /// <summary>
    /// Creates a new instance of <see cref="DaprSecretStoreConfigurationProvider"/>.
    /// </summary>
    /// <param name="store">Dapr Secret Store name.</param>
    /// <param name="normalizeKey">Indicates whether any key delimiters should be replaced with the delimiter ":".</param>
    /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the secret store. The valid metadata keys and values are determined by the type of secret store used.</param>
    /// <param name="client">Dapr client used to retrieve Secrets</param>
    public DaprSecretStoreConfigurationProvider(
        string store,
        bool normalizeKey,
        IReadOnlyDictionary<string, string>? metadata,
        DaprClient client) : this(store, normalizeKey, null, metadata, client, DefaultSidecarWaitTimeout)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="DaprSecretStoreConfigurationProvider"/>.
    /// </summary>
    /// <param name="store">Dapr Secret Store name.</param>
    /// <param name="normalizeKey">Indicates whether any key delimiters should be replaced with the delimiter ":".</param>
    /// <param name="keyDelimiters">A collection of delimiters that will be replaced by ':' in the key of every secret.</param>
    /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the secret store. The valid metadata keys and values are determined by the type of secret store used.</param>
    /// <param name="client">Dapr client used to retrieve Secrets</param>
    public DaprSecretStoreConfigurationProvider(
        string store,
        bool normalizeKey,
        IList<string>? keyDelimiters,
        IReadOnlyDictionary<string, string>? metadata,
        DaprClient client) : this(store, normalizeKey, keyDelimiters, metadata, client, DefaultSidecarWaitTimeout)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="DaprSecretStoreConfigurationProvider"/>.
    /// </summary>
    /// <param name="store">Dapr Secret Store name.</param>
    /// <param name="normalizeKey">Indicates whether any key delimiters should be replaced with the delimiter ":".</param>
    /// <param name="keyDelimiters">A collection of delimiters that will be replaced by ':' in the key of every secret.</param>
    /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the secret store. The valid metadata keys and values are determined by the type of secret store used.</param>
    /// <param name="client">Dapr client used to retrieve Secrets</param>
    /// <param name="sidecarWaitTimeout">The <see cref="TimeSpan"/> used to configure the timeout waiting for Dapr.</param>
    public DaprSecretStoreConfigurationProvider(
        string store,
        bool normalizeKey,
        IList<string>? keyDelimiters,
        IReadOnlyDictionary<string, string>? metadata,
        DaprClient client,
        TimeSpan sidecarWaitTimeout)
    {
        ArgumentVerifier.ThrowIfNullOrEmpty(store, nameof(store));
        ArgumentVerifier.ThrowIfNull(client, nameof(client));

        this.store = store;
        this.normalizeKey = normalizeKey;
        this.keyDelimiters = keyDelimiters;
        this.metadata = metadata;
        this.client = client;
        this.sidecarWaitTimeout = sidecarWaitTimeout;
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

    /// <summary>
    /// Loads the configuration by calling the asynchronous LoadAsync method and blocking the calling
    /// thread until the operation is completed.
    /// </summary>
    public override void Load() => LoadAsync().ConfigureAwait(false).GetAwaiter().GetResult();

    private async Task LoadAsync()
    {
        var data = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        // Wait for the Dapr Sidecar to report healthy before attempting to fetch secrets.
        using (var tokenSource = new CancellationTokenSource(sidecarWaitTimeout))
        {
            await client.WaitForSidecarAsync(tokenSource.Token);
        }

        if (secretDescriptors != null)
        {
            foreach (var secretDescriptor in secretDescriptors)
            {

                Dictionary<string, string> result;

                try
                {
                    result = await client
                        .GetSecretAsync(store, secretDescriptor.SecretKey, secretDescriptor.Metadata)
                        .ConfigureAwait(false);
                }
                catch (DaprException)
                {
                    if (secretDescriptor.IsRequired)
                    {
                        throw;
                    }
                    result = new Dictionary<string, string>();
                }

                foreach (var key in result.Keys)
                {
                    if (data.ContainsKey(key))
                    {
                        throw new InvalidOperationException(
                            $"A duplicate key '{key}' was found in the secret store '{store}'. Please remove any duplicates from your secret store.");
                    }

                    // The name of the key "as desired" by the user based on the descriptor.
                    //
                    // NOTE: This should vary only if a single secret of the same name is returned.
                    string desiredKey = StringComparer.Ordinal.Equals(key, secretDescriptor.SecretKey) ? secretDescriptor.SecretName : key;

                    // The name of the key normalized based on the configured delimiters.
                    string normalizedKey = normalizeKey ? NormalizeKey(desiredKey) : desiredKey;

                    data.Add(normalizedKey, result[key]);
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