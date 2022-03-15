using System;
using System.Collections.Generic;
using System.Threading;
using Dapr.Client;
using Microsoft.Extensions.Configuration;

namespace Dapr.Extensions.Configuration
{
    /// <summary>
    /// Configuration source that provides a <see cref="DaprConfigurationStoreProvider"/>.
    /// </summary>
    [Obsolete]
    public class DaprConfigurationStoreSource : IConfigurationSource
    {
        /// <summary>
        /// The Configuration Store to query.
        /// </summary>
        public string Store { get; set; } = default!;

        /// <summary>
        /// The list of keys to request from the configuration. If empty, request all keys.
        /// </summary>
        public IReadOnlyList<string> Keys { get; set; } = default!;

        /// <summary>
        /// The <see cref="DaprClient"/> used to query the configuration.
        /// </summary>
        public DaprClient Client { get; set; } = default!;

        /// <summary>
        /// Boolean stating if this is a streaming call or not.
        /// </summary>
        public bool IsStreaming { get; set; } = false;

        /// <summary>
        /// Gets or sets the <see cref="TimeSpan"/> that is used to control the timeout waiting for the Dapr sidecar to become healthly.
        /// </summary>
        public TimeSpan SidecarWaitTimeout { get; set; }

        /// <summary>
        /// The optional metadata to be sent to the configuration store.
        /// </summary>
        public IReadOnlyDictionary<string, string>? Metadata { get; set; } = default;

        /// <summary>
        /// The <see cref="CancellationToken"/> that is used to cancel the request.
        /// </summary>
        public CancellationToken CancellationToken { get; set; } = default;

        /// <inheritdoc/>
        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new DaprConfigurationStoreProvider(Store, Keys, Client, SidecarWaitTimeout, IsStreaming, Metadata, CancellationToken);
        }
    }
}
