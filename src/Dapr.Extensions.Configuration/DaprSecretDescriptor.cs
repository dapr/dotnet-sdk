// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Collections.Generic;

namespace Dapr.Extensions.Configuration
{
    /// <summary>
    /// Represents the name and metadata for a Secret.
    /// </summary>
    public class DaprSecretDescriptor
    {
        /// <summary>
        /// Gets or sets the secret name.
        /// </summary>
        public string SecretName { get; }

        /// <summary>
        /// A collection of metadata key-value pairs that will be provided to the secret store. The valid metadata keys and values are determined by the type of secret store used.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; }

        /// <summary>
        /// Secret Descriptor Construcutor
        /// </summary>
        public DaprSecretDescriptor(string secretName) : this(secretName, new Dictionary<string, string>())
        {

        }

        /// <summary>
        /// Secret Descriptor Construcutor
        /// </summary>
        public DaprSecretDescriptor(string secretName, IReadOnlyDictionary<string, string> metadata)
        {
            SecretName = secretName;
            Metadata = metadata;
        }
    }
}