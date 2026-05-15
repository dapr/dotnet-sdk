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
/// Marks an interface as a typed accessor for a Dapr secret store. When applied to a <c>partial interface</c>,
/// the Dapr Secrets Management source generator will produce a concrete implementation that retrieves secrets
/// from the specified Dapr secret store component and registers it in the dependency injection container.
/// </summary>
/// <remarks>
/// <para>
/// The interface should declare <see langword="string"/>-typed read-only properties. Each property maps to a
/// single secret key in the store. The key name defaults to the property name but can be overridden with
/// <see cref="SecretAttribute"/>.
/// </para>
/// <para>
/// Generated implementations load all mapped secrets in bulk at startup via an <c>IHostedService</c> and
/// expose them as synchronous properties. This makes secrets available immediately after host startup without
/// requiring callers to manage async flows.
/// </para>
/// <example>
/// <code>
/// [SecretStore("my-vault")]
/// public partial interface IMySecrets
/// {
///     /// &lt;summary&gt;The database connection string.&lt;/summary&gt;
///     [Secret("db-connection-string")]
///     string DatabaseConnection { get; }
///
///     /// &lt;summary&gt;The API key (uses property name as secret key).&lt;/summary&gt;
///     string ApiKey { get; }
/// }
/// </code>
/// </example>
/// </remarks>
[AttributeUsage(AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
public sealed class SecretStoreAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SecretStoreAttribute"/> class.
    /// </summary>
    /// <param name="storeName">
    /// The name of the Dapr secret store component to retrieve secrets from. This must match the
    /// <c>metadata.name</c> of a configured Dapr secret store component.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="storeName"/> is <see langword="null"/>.</exception>
    public SecretStoreAttribute(string storeName)
    {
        ArgumentNullException.ThrowIfNull(storeName);
        StoreName = storeName;
    }

    /// <summary>
    /// Gets the name of the Dapr secret store component that secrets will be retrieved from.
    /// </summary>
    public string StoreName { get; }
}
