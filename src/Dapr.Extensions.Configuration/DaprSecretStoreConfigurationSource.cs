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
        public string Store { get; set; } = default!;

        /// <summary>
        /// Gets or sets a value indicating whether any key delimiters should be replaced with the delimiter ":".
        /// Default value true.
        /// </summary>
        public bool NormalizeKey { get; set; } = true;

        /// <summary>
        /// Gets or sets the custom key delimiters. Contains the '__' delimiter by default.
        /// </summary>
        public IList<string> KeyDelimiters { get; set; } = new List<string> { "__" };

        /// <summary>
        /// Gets or sets the secret descriptors.
        /// </summary>
        public IEnumerable<DaprSecretDescriptor>? SecretDescriptors { get; set; }

        /// <summary>
        /// A collection of metadata key-value pairs that will be provided to the secret store. The valid metadata keys and values are determined by the type of secret store used.
        /// </summary>
        public IReadOnlyDictionary<string, string>? Metadata { get; set; }

        /// <summary>
        /// Gets or sets the http client.
        /// </summary>
        public DaprClient Client { get; set; } = default!;

        /// <inheritdoc />
        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            if (SecretDescriptors != null)
            {
                if (Metadata != null)
                {
                    throw new ArgumentException($"{nameof(Metadata)} must be null when {nameof(SecretDescriptors)} is set", nameof(Metadata));
                }

                return new DaprSecretStoreConfigurationProvider(Store, NormalizeKey, KeyDelimiters, SecretDescriptors, Client);
            }
            else
            {
                return new DaprSecretStoreConfigurationProvider(Store, NormalizeKey, KeyDelimiters, Metadata, Client);
            }
        }
    }
}
