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
        /// Gets or sets the secret's metadata.
        /// </summary>
        public Dictionary<string, string> Metadata { get; }

        /// <summary>
        /// Secret Descriptor Construcutor
        /// </summary>
        public DaprSecretDescriptor(string secretName) : this(secretName, new Dictionary<string, string>())
        {

        }

        /// <summary>
        /// Secret Descriptor Construcutor
        /// </summary>
        public DaprSecretDescriptor(string secretName, Dictionary<string, string> metadata)
        {
            SecretName = secretName;
            Metadata = metadata;
        }
    }
}