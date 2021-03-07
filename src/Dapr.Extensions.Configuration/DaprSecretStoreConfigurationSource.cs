// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
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
        /// Gets or sets if replace "__" with delimiter ":".
        /// Default value true.
        /// </summary>
        public bool NormalizeKey { get; set; } = true;

        /// <summary>
        /// Gets or sets the secret descriptors.
        /// </summary>
        public IEnumerable<DaprSecretDescriptor> SecretDescriptors { get; set; }

        /// <summary>
        /// A collection of metadata key-value pairs that will be provided to the secret store. The valid metadata keys and values are determined by the type of secret store used.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; set; }

        /// <summary>
        /// Gets or sets the http client.
        /// </summary>
        public DaprClient Client { get; set; }

        /// <inheritdoc />
        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            if (SecretDescriptors != null)
            {
                if (Metadata != null)
                {
                    throw new ArgumentException($"{nameof(Metadata)} must be null when {nameof(SecretDescriptors)} is set", nameof(Metadata));
                }

                return new DaprSecretStoreConfigurationProvider(Store, NormalizeKey, SecretDescriptors, Client);
            }
            else
            {
                return new DaprSecretStoreConfigurationProvider(Store, NormalizeKey, Metadata, Client);
            }
        }
    }
}
