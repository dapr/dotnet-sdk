// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Collections.Generic;
using Dapr.Client;
using Microsoft.Extensions.Configuration;

namespace Dapr.Extensions.Configuration.DaprSecretStore
{
    /// <summary>
    /// Represents Dapr Secret Store as an <see cref="IConfigurationSource"/>.
    /// </summary>
    public class DaprSecretStoreConfigurationSource : IConfigurationSource
    {
        /// <summary>
        /// Gets or sets the store name.
        /// </summary>
        public string Store { get; set; }

        /// <summary>
        /// Gets or sets the secret names.
        /// </summary>
        public IEnumerable<DaprSecretDescriptor> SecretDescriptors { get; set; }

        /// <summary>
        /// Gets or sets the http client.
        /// </summary>
        public Dapr.Client.DaprClient Client { get; set; }

        /// <inheritdoc />
        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new DaprSecretStoreConfigurationProvider(Store, SecretDescriptors, Client);
        }
    }
}
