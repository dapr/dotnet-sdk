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
        /// This flag indicates if this field's existence should trigger an exception. Setting it to "false"
        /// will suppress the exception whereas setting it to "true" will not suppress it.
        /// </summary>
        public bool IsRequired { get; }

        /// <summary>
        /// SecretKey value is mapping value to Vault Name. If The application's Secret Name and Secret in the
        /// Vault Name is different then you can use this flag to specify Vault Secret Name. 
        /// </summary>
        public string SecretKey { get; }

        /// <summary>
        /// Secret Descriptor Constructor
        /// </summary>
        public DaprSecretDescriptor(string secretName) : this(secretName, new Dictionary<string, string>())
        {

        }

        /// <summary>
        /// Secret Descriptor Constructor
        /// </summary>
        public DaprSecretDescriptor(string secretName, IReadOnlyDictionary<string, string> metadata) :
            this(secretName, metadata, true, secretName)
        {

        }

        /// <summary>
        /// Secret Descriptor Constructor
        /// </summary>
        public DaprSecretDescriptor(string secretName, IReadOnlyDictionary<string, string> metadata, bool isRequired) :
            this(secretName, metadata, isRequired, secretName)
        {
        }

        /// <summary>
        /// Secret Descriptor Constructor
        /// </summary>
        public DaprSecretDescriptor(string secretName, IReadOnlyDictionary<string, string> metadata, bool isRequired, string secretKey)
        {
            SecretName = secretName;
            Metadata = metadata;
            IsRequired = isRequired;
            SecretKey = secretKey;
        }
    }
}
