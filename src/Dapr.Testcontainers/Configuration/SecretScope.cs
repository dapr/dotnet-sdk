// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//  ------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dapr.Testcontainers.Configuration;

/// <summary>
/// Builds out a collection of secret scopes to the Dapr configuration.
/// </summary>
/// <param name="scopes">The secret scopes to emit.</param>
public sealed class SecretScopeConfigurationBuilder(IReadOnlyCollection<SecretScope> scopes)
{
	/// <summary>
	/// Builds the secret scope section of the Dapr configuration file.
	/// </summary>
	/// <returns></returns>
	public string Build()
	{
		var sb = new StringBuilder("secrets:");
		sb.AppendLine("  scopes:");
		foreach (var scope in scopes)
		{
			sb.AppendLine($"    - storeName: {scope.StoreName}");
			sb.AppendLine($"      defaultAccess: {scope.DefaultAccess.ToString().ToLowerInvariant()}");
			if (scope.AllowedSecrets.Count > 0)
			{
				sb.AppendLine(
					$"      allowedSecrets: [{string.Join(", ", scope.AllowedSecrets.Select(s => $"\"{s}\""))}]");
			}
			if (scope.DeniedSecrets.Count > 0)
			{
				sb.AppendLine(
					$"      deniedSecrets: [{string.Join(", ", scope.DeniedSecrets.Select(s => $"\"{s}\""))}]");
			}
		}

		return sb.ToString();
	}
}

/// <summary>
/// Used to scope a named secret store component to one or more secrets for an application.
/// </summary>
/// <param name="StoreName">The name of the secret store.</param>
/// <param name="DefaultAccess">The default access to the secrets in the store.</param>
/// <param name="AllowedSecrets">The list of secret keys that can be accessed.</param>
/// <param name="DeniedSecrets">The list of secret keys that cannot be accessed.</param>
public sealed record SecretScope(
	string StoreName,
	SecretStoreAccess DefaultAccess,
	IReadOnlyCollection<string> AllowedSecrets,
	IReadOnlyCollection<string> DeniedSecrets);

/// <summary>
/// The secret store access modifier.
/// </summary>
public enum SecretStoreAccess
{
    /// <summary>
    /// Indicates that secret access should be allowed by default.
    /// </summary>
	Allow,
    /// <summary>
    /// Indicates that secret access should be denied by default.
    /// </summary>
	Deny
}
