using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Dapr.Extensions.Configuration
{
    /// <summary>
    /// A configuration provider that utilizes the Dapr Configuration API. It can either be a single, constant
    /// call or a streaming call.
    /// </summary>
    [Obsolete]
    public class DaprConfigurationStoreProvider : ConfigurationProvider
    {
        private string store { get; }
        private IReadOnlyList<string> keys { get; }
        private DaprClient daprClient { get; }
        private TimeSpan sidecarWaitTimeout { get; }
        private bool isStreaming { get; } = false;
        private IReadOnlyDictionary<string, string>? metadata { get; } = default;
        private CancellationToken cancellationToken { get; } = default;
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
        /// <param name="cancellationToken"><see cref="CancellationToken"/> used to cancel the operation.</param>
        public DaprConfigurationStoreProvider(
            string store,
            IReadOnlyList<string> keys,
            DaprClient daprClient,
            TimeSpan sidecarWaitTimeout,
            bool isStreaming = false,
            IReadOnlyDictionary<string, string>? metadata = default,
            CancellationToken cancellationToken = default)
        {
            this.store = store;
            this.keys = keys;
            this.daprClient = daprClient;
            this.sidecarWaitTimeout = sidecarWaitTimeout;
            this.isStreaming = isStreaming;
            this.metadata = metadata;
            this.cancellationToken = cancellationToken;
        }

        /// <inheritdoc/>
        public override bool TryGet(string key, out string? value)
        {
            return Data.TryGetValue(key, out value);
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
                var subscribeConfigurationResponse = await daprClient.SubscribeConfiguration(store, keys, metadata, cancellationToken);
                subscribeTask = Task.Run(async () =>
                {
                    await foreach (var items in subscribeConfigurationResponse.Source)
                    {
                        // Whenever we get an update, make sure to update the reloadToken.
                        OnReload();
                        foreach (var item in items)
                        {
                            Set(item.Key, item.Value);
                        }
                    }
                }, cancellationToken);                
            }
            else
            {
                // We don't need to worry about ReloadTokens here because it is a constant response.
                var getConfigurationResponse = await daprClient.GetConfiguration(store, keys, metadata, cancellationToken);
                foreach (var item in getConfigurationResponse.Items)
                {
                    Set(item.Key, item.Value);
                }
            }
        }
    }
}
