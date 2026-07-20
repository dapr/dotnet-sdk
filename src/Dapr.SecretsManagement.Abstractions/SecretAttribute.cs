// ------------------------------------------------------------------------
//  Copyright 2026 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

namespace Dapr.SecretsManagement.Abstractions;

/// <summary>
/// Specifies the secret key name used when retrieving the value of the annotated property from a Dapr
/// secret store. When this attribute is omitted, the property name is used as the secret key.
/// </summary>
/// <remarks>
/// This attribute should be applied to properties on an interface that is also annotated with
/// <see cref="SecretStoreAttribute"/>. The source generator uses this metadata to map each property
/// to the correct secret key during bulk secret retrieval.
/// </remarks>
/// <example>
/// <code>
/// [SecretStore("my-vault")]
/// public partial interface IMySecrets
/// {
///     [Secret("database-connection-string")]
///     string DbConnection { get; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class SecretAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SecretAttribute"/> class.
    /// </summary>
    /// <param name="secretName">
    /// The name of the secret key in the Dapr secret store. This value is used when calling the
    /// Dapr Secrets API to retrieve the secret value.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="secretName"/> is <see langword="null"/>.</exception>
    public SecretAttribute(string secretName)
    {
        ArgumentNullException.ThrowIfNull(secretName);
        SecretName = secretName;
    }

    /// <summary>
    /// Gets the name of the secret key in the Dapr secret store.
    /// </summary>
    public string SecretName { get; }
}
