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
using System.Threading.Tasks;
using Dapr.Client;
using Microsoft.Extensions.Configuration;

namespace Dapr.Extensions.Configuration;

/// <summary>
/// A configuration provider that utilizes the Dapr Configuration API. It can either be a single, constant
/// call or a streaming call.
/// </summary>
internal class DaprConfigurationStoreProvider : ConfigurationProvider, IDisposable
{
    private string store;
    private IReadOnlyList<string> keys;
    private DaprClient daprClient;
    private TimeSpan sidecarWaitTimeout;
    private bool isStreaming;
    private IReadOnlyDictionary<string, string>? metadata;
    private CancellationTokenSource cts;
    private Task subscribeTask = Task.CompletedTask;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="store">The configuration store to query.</param>
    /// <param name="keys">The keys, if any, to request. If empty, returns all configuration items.</param>
    /// <param name="daprClient">The <see cref="DaprClient"/> used for the request.</param>
    /// <param name="sidecarWaitTimeout">The <see cref="TimeSpan"/> used to configure the timeout waiting for Dapr.</param>
    /// <param name="isStreaming">Determines if the source is streaming or not.</param>
    /// <param name="metadata">Optional metadata sent to the configuration store.</param>
    public DaprConfigurationStoreProvider(
        string store,
        IReadOnlyList<string> keys,
        DaprClient daprClient,
        TimeSpan sidecarWaitTimeout,
        bool isStreaming = false,
        IReadOnlyDictionary<string, string>? metadata = default)
    {
        this.store = store;
        this.keys = keys;
        this.daprClient = daprClient;
        this.sidecarWaitTimeout = sidecarWaitTimeout;
        this.isStreaming = isStreaming;
        this.metadata = metadata ?? new Dictionary<string, string>();
        this.cts = new CancellationTokenSource();
    }

    public void Dispose()
    {
        cts.Cancel();
    }

    /// <inheritdoc/>
    public override void Load() => LoadAsync().ConfigureAwait(false).GetAwaiter().GetResult();

    private async Task LoadAsync()
    {
        // Wait for the sidecar to become available.
        using (var tokenSource = new CancellationTokenSource(sidecarWaitTimeout))
        {
            await daprClient.WaitForSidecarAsync(tokenSource.Token);
        }

        if (isStreaming)
        {
            subscribeTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var id = string.Empty;
                    try
                    {
                        var subscribeConfigurationResponse = await daprClient.SubscribeConfiguration(store, keys, metadata, cts.Token);
                        await foreach (var items in subscribeConfigurationResponse.Source.WithCancellation(cts.Token))
                        {
                            var data = new Dictionary<string, string>(Data, StringComparer.OrdinalIgnoreCase);
                            foreach (var item in items)
                            {
                                id = subscribeConfigurationResponse.Id;
                                data[item.Key] = item.Value.Value;
                            }
                            Data = data;
                            // Whenever we get an update, make sure to update the reloadToken.
                            OnReload();
                        }
                    }
                    catch (Exception)
                    {
                        // If we catch an exception, try and cancel the subscription so we can connect again.
                        if (!string.IsNullOrEmpty(id))
                        {
                            await daprClient.UnsubscribeConfiguration(store, id);
                        }
                    }
                }
            });
        }
        else
        {
            // We don't need to worry about ReloadTokens here because it is a constant response.
            var getConfigurationResponse = await daprClient.GetConfiguration(store, keys, metadata, cts.Token);
            foreach (var item in getConfigurationResponse.Items)
            {
                Set(item.Key, item.Value.Value);
            }
        }
    }
}