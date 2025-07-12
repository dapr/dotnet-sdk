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
using System.Threading;
using Dapr.Client;
using Microsoft.Extensions.Configuration;

namespace Dapr.Extensions.Configuration;

/// <summary>
/// Extension used to call the Dapr Configuration API and store the values in a <see cref="IConfiguration"/>.
/// </summary>
public static class DaprConfigurationStoreExtension
{
    /// <summary>
    /// Register a constant configuration store. This will make one call to Dapr with the given keys to the
    /// Configuration API.
    /// </summary>
    /// <param name="configurationBuilder">The <see cref="IConfigurationBuilder"/> to add to.</param>
    /// <param name="store">The configuration store to query.</param>
    /// <param name="keys">The keys, if any, to request. If empty, returns all configuration items.</param>
    /// <param name="client">The <see cref="DaprClient"/> used for the request.</param>
    /// <param name="sidecarWaitTimeout">The <see cref="TimeSpan"/> used to configure the timeout waiting for Dapr.</param>
    /// <param name="metadata">Optional metadata sent to the configuration store.</param>
    /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
    public static IConfigurationBuilder AddDaprConfigurationStore(
        this IConfigurationBuilder configurationBuilder,
        string store,
        IReadOnlyList<string> keys,
        DaprClient client,
        TimeSpan sidecarWaitTimeout,
        IReadOnlyDictionary<string, string>? metadata = default)
    {
        ArgumentVerifier.ThrowIfNullOrEmpty(store, nameof(store));
        ArgumentVerifier.ThrowIfNull(keys, nameof(keys));
        ArgumentVerifier.ThrowIfNull(client, nameof(client));

        configurationBuilder.Add(new DaprConfigurationStoreSource()
        {
            Store = store,
            Keys = keys,
            Client = client,
            SidecarWaitTimeout = sidecarWaitTimeout,
            IsStreaming = false,
            Metadata = metadata
        });

        return configurationBuilder;
    }

    /// <summary>
    /// Register a streaming configuration store. This opens a stream to the Dapr Configuration API which
    /// will get updates to keys should any occur. This stream will not be closed until canceled with the
    /// <see cref="CancellationToken"/> or the configuration is unsubscribed in Dapr.
    /// </summary>
    /// <param name="configurationBuilder">The <see cref="IConfigurationBuilder"/> to add to.</param>
    /// <param name="store">The configuration store to query.</param>
    /// <param name="keys">The keys, if any, to request. If empty, returns all configuration items.</param>
    /// <param name="client">The <see cref="DaprClient"/> used for the request.</param>
    /// <param name="sidecarWaitTimeout">The <see cref="TimeSpan"/> used to configure the timeout waiting for Dapr.</param>
    /// <param name="metadata">Optional metadata sent to the configuration store.</param>
    /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
    public static IConfigurationBuilder AddStreamingDaprConfigurationStore(
        this IConfigurationBuilder configurationBuilder,
        string store,
        IReadOnlyList<string> keys,
        DaprClient client,
        TimeSpan sidecarWaitTimeout,
        IReadOnlyDictionary<string, string>? metadata = default)
    {
        ArgumentVerifier.ThrowIfNullOrEmpty(store, nameof(store));
        ArgumentVerifier.ThrowIfNull(keys, nameof(keys));
        ArgumentVerifier.ThrowIfNull(client, nameof(client));

        configurationBuilder.Add(new DaprConfigurationStoreSource()
        {
            Store = store,
            Keys = keys,
            Client = client,
            SidecarWaitTimeout = sidecarWaitTimeout,
            IsStreaming = true,
            Metadata = metadata
        });

        return configurationBuilder;
    }
}