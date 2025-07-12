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

namespace Dapr.Extensions.Configuration.DaprSecretStore;

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

    /// <summary>
    /// Gets or sets the <see cref="TimeSpan"/> that is used to control the timeout waiting for the Dapr sidecar to become healthly.
    /// </summary>
    public TimeSpan? SidecarWaitTimeout { get; set; }

    /// <inheritdoc />
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        if (SecretDescriptors != null)
        {
            if (Metadata != null)
            {
                throw new ArgumentException($"{nameof(Metadata)} must be null when {nameof(SecretDescriptors)} is set", nameof(Metadata));
            }

            return new DaprSecretStoreConfigurationProvider(Store, NormalizeKey, KeyDelimiters, SecretDescriptors, Client, SidecarWaitTimeout ?? DaprSecretStoreConfigurationProvider.DefaultSidecarWaitTimeout);
        }
        else
        {
            return new DaprSecretStoreConfigurationProvider(Store, NormalizeKey, KeyDelimiters, Metadata, Client, SidecarWaitTimeout ?? DaprSecretStoreConfigurationProvider.DefaultSidecarWaitTimeout);
        }
    }
}