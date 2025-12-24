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

using System.IO;

namespace Dapr.TestContainers.Containers;

/// <summary>
/// This is an odd one because it's not actually a container - just the entry point for the YAML file builder.
/// </summary>
public sealed class LocalStorageCryptographyContainer
{
	/// <summary>
	/// Builds out the YAML components for the local storage cryptography implementation.
	/// </summary>
	public static class Yaml
	{
        /// <summary>
        /// Writes a 
        /// </summary>
        /// <param name="folderPath"></param>
        /// <param name="keyPath"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
		public static string WriteCryptoYamlToFolder(string folderPath, string keyPath, string fileName = "local-crypto.yaml")
		{
			var yaml = GetLocalStorageYaml(keyPath);
			return WriteToFolder(folderPath, fileName, yaml);
		}
		
		private static string WriteToFolder(string folderPath, string fileName, string yaml)
		{
			Directory.CreateDirectory(folderPath);
			var fullPath = Path.Combine(folderPath, fileName);
			File.WriteAllText(fullPath, yaml);
			return fullPath;
		}

		private static string GetLocalStorageYaml(string keyPath) =>
			$@"apiVersion: dapr.io/v1alpha
kind: Component
metadata:
  name: {Constants.DaprComponentNames.CryptographyComponentName}
spec:
  type: crypto.dapr.localstorage
  version: v1
  metadata:
    - name: path
      value: {keyPath}";
	}
}
