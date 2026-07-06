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

namespace Dapr.Extensions.Configuration;

/// <summary>
/// Represents the name and metadata for a Secret.
/// </summary>
public class DaprSecretDescriptor
{
    /// <summary>
    /// The name of the secret to retrieve from the Dapr secret store.
    /// </summary>
    /// <remarks>
    /// If the <see cref="SecretKey"/> is not specified, this value will also be used as the key to retrieve the secret from the associated source secret store.
    /// </remarks>
    public string SecretName { get; }

    /// <summary>
    /// A collection of metadata key-value pairs that will be provided to the secret store. The valid metadata keys and values are determined by the type of secret store used.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>
    /// A value indicating whether to throw an exception if the secret is not found in the source secret store.
    /// </summary>
    /// <remarks>
    /// Setting this value to <see langword="false"/> will suppress the exception; otherwise, <see langword="true"/> will not.
    /// </remarks>
    public bool IsRequired { get; }

    /// <summary>
    /// The secret key that maps to the <see cref="SecretName"/> to retrieve from the source secret store.
    /// </summary>
    /// <remarks>
    /// Use this property when the <see cref="SecretName"/> does not match the key used to retrieve the secret from the source secret store.
    /// </remarks>
    public string SecretKey { get; }

    /// <summary>
    /// Secret Descriptor Constructor
    /// </summary>
    public DaprSecretDescriptor(string secretName, bool isRequired = true, string secretKey = "") 
        : this(secretName, new Dictionary<string, string>(), isRequired, secretKey)
    {

    }

    /// <summary>
    /// Secret Descriptor Constructor
    /// </summary>
    public DaprSecretDescriptor(string secretName, IReadOnlyDictionary<string, string> metadata, bool isRequired = true, string secretKey = "")
    {
        SecretName = secretName;
        Metadata = metadata;
        IsRequired = isRequired;
        SecretKey = string.IsNullOrEmpty(secretKey) ? secretName : secretKey;
    }
}