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
using Dapr.Client;
using Microsoft.Extensions.Configuration;
using Dapr.Extensions.Configuration.DaprSecretStore;
using System.Linq;

namespace Dapr.Extensions.Configuration;

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
        DaprClient client)
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
    /// Adds an <see cref="IConfigurationProvider"/> that reads configuration values from the Dapr Secret Store.
    /// </summary>
    /// <param name="configurationBuilder">The <see cref="IConfigurationBuilder"/> to add to.</param>
    /// <param name="store">Dapr secret store name.</param>
    /// <param name="secretDescriptors">The secrets to retrieve.</param>
    /// <param name="client">The Dapr client.</param>
    /// <param name="sidecarWaitTimeout">The <see cref="TimeSpan"/> used to configure the timeout waiting for Dapr.</param>
    /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
    public static IConfigurationBuilder AddDaprSecretStore(
        this IConfigurationBuilder configurationBuilder,
        string store,
        IEnumerable<DaprSecretDescriptor> secretDescriptors,
        DaprClient client,
        TimeSpan sidecarWaitTimeout)
    {
        ArgumentVerifier.ThrowIfNullOrEmpty(store, nameof(store));
        ArgumentVerifier.ThrowIfNull(secretDescriptors, nameof(secretDescriptors));
        ArgumentVerifier.ThrowIfNull(client, nameof(client));

        configurationBuilder.Add(new DaprSecretStoreConfigurationSource()
        {
            Store = store,
            SecretDescriptors = secretDescriptors,
            Client = client,
            SidecarWaitTimeout = sidecarWaitTimeout
        });

        return configurationBuilder;
    }

    /// <summary>
    /// Adds an <see cref="IConfigurationProvider"/> that reads configuration values from the Dapr Secret Store.
    /// </summary>
    /// <param name="configurationBuilder">The <see cref="IConfigurationBuilder"/> to add to.</param>
    /// <param name="store">Dapr secret store name.</param>
    /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the secret store. The valid metadata keys and values are determined by the type of secret store used.</param>
    /// <param name="client">The Dapr client</param>
    /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
    public static IConfigurationBuilder AddDaprSecretStore(
        this IConfigurationBuilder configurationBuilder,
        string store,
        DaprClient client,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentVerifier.ThrowIfNullOrEmpty(store, nameof(store));
        ArgumentVerifier.ThrowIfNull(client, nameof(client));

        configurationBuilder.Add(new DaprSecretStoreConfigurationSource()
        {
            Store = store,
            Metadata = metadata,
            Client = client
        });

        return configurationBuilder;
    }

    /// <summary>
    /// Adds an <see cref="IConfigurationProvider"/> that reads configuration values from the Dapr Secret Store.
    /// </summary>
    /// <param name="configurationBuilder">The <see cref="IConfigurationBuilder"/> to add to.</param>
    /// <param name="store">Dapr secret store name.</param>
    /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the secret store. The valid metadata keys and values are determined by the type of secret store used.</param>
    /// <param name="client">The Dapr client</param>
    /// <param name="sidecarWaitTimeout">The <see cref="TimeSpan"/> used to configure the timeout waiting for Dapr.</param>
    /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
    public static IConfigurationBuilder AddDaprSecretStore(
        this IConfigurationBuilder configurationBuilder,
        string store,
        DaprClient client,
        TimeSpan sidecarWaitTimeout,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentVerifier.ThrowIfNullOrEmpty(store, nameof(store));
        ArgumentVerifier.ThrowIfNull(client, nameof(client));

        configurationBuilder.Add(new DaprSecretStoreConfigurationSource()
        {
            Store = store,
            Metadata = metadata,
            Client = client,
            SidecarWaitTimeout = sidecarWaitTimeout
        });

        return configurationBuilder;
    }

    /// <summary>
    /// Adds an <see cref="IConfigurationProvider"/> that reads configuration values from the Dapr Secret Store.
    /// </summary>
    /// <param name="configurationBuilder">The <see cref="IConfigurationBuilder"/> to add to.</param>
    /// <param name="store">Dapr secret store name.</param>
    /// <param name="keyDelimiters">A collection of delimiters that will be replaced by ':' in the key of every secret.</param>
    /// <param name="client">The Dapr client</param>
    /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
    public static IConfigurationBuilder AddDaprSecretStore(
        this IConfigurationBuilder configurationBuilder,
        string store,
        DaprClient client,
        IEnumerable<string>? keyDelimiters)
    {
        ArgumentVerifier.ThrowIfNullOrEmpty(store, nameof(store));
        ArgumentVerifier.ThrowIfNull(client, nameof(client));

        var source = new DaprSecretStoreConfigurationSource
        {
            Store = store,
            Client = client
        };

        if (keyDelimiters != null)
        {
            source.KeyDelimiters = keyDelimiters.ToList();
        }

        configurationBuilder.Add(source);

        return configurationBuilder;
    }

    /// <summary>
    /// Adds an <see cref="IConfigurationProvider"/> that reads configuration values from the Dapr Secret Store.
    /// </summary>
    /// <param name="configurationBuilder">The <see cref="IConfigurationBuilder"/> to add to.</param>
    /// <param name="store">Dapr secret store name.</param>
    /// <param name="keyDelimiters">A collection of delimiters that will be replaced by ':' in the key of every secret.</param>
    /// <param name="client">The Dapr client</param>
    /// <param name="sidecarWaitTimeout">The <see cref="TimeSpan"/> used to configure the timeout waiting for Dapr.</param>
    /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
    public static IConfigurationBuilder AddDaprSecretStore(
        this IConfigurationBuilder configurationBuilder,
        string store,
        DaprClient client,
        IEnumerable<string>? keyDelimiters,
        TimeSpan sidecarWaitTimeout)
    {
        ArgumentVerifier.ThrowIfNullOrEmpty(store, nameof(store));
        ArgumentVerifier.ThrowIfNull(client, nameof(client));

        var source = new DaprSecretStoreConfigurationSource
        {
            Store = store,
            Client = client,
            SidecarWaitTimeout = sidecarWaitTimeout
        };

        if (keyDelimiters != null)
        {
            source.KeyDelimiters = keyDelimiters.ToList();
        }

        configurationBuilder.Add(source);

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