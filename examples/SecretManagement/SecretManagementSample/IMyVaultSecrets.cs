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

using Dapr.SecretsManagement.Abstractions;

namespace SecretManagementSample;

/// <summary>
/// Example of a typed secret store interface. Apply the <see cref="SecretStoreAttribute"/> to an interface
/// and the Dapr Secrets Management source generator will produce:
///   1. A concrete implementation that caches secrets loaded at startup.
///   2. A DI registration extension method (e.g., <c>AddMyVaultSecrets()</c>).
///
/// Properties without <see cref="SecretAttribute"/> use the property name as the secret key.
/// Properties with <see cref="SecretAttribute"/> use the specified secret name.
/// </summary>
[SecretStore("my-vault")]
public partial interface IMyVaultSecrets
{
    /// <summary>
    /// The database connection string, retrieved from the "db-connection-string" secret key.
    /// </summary>
    [Secret("db-connection-string")]
    string DatabaseConnection { get; }

    /// <summary>
    /// The API key. Uses the property name "ApiKey" as the secret key.
    /// </summary>
    string ApiKey { get; }
}
