// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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

using System.IO;

namespace Dapr.Testcontainers.Containers;

/// <summary>
/// Not a real container - the entry point for the in-memory binary store component YAML builder.
/// </summary>
public sealed class InMemoryBinaryStoreContainer
{
    /// <summary>
    /// Builds the YAML component for the in-memory binary store.
    /// </summary>
    public static class Yaml
    {
        /// <summary>
        /// Writes the component YAML.
        /// </summary>
        /// <param name="folderPath">The directory to write to.</param>
        /// <param name="maxSize">Optional maximum size in bytes of a single file.</param>
        /// <param name="maxTotalSize">Optional maximum total size in bytes of the store.</param>
        /// <param name="fileName">The yaml file name.</param>
        public static void WriteBinaryStoreYamlToFolder(string folderPath, long? maxSize = null, long? maxTotalSize = null, string fileName = "binarystore.yaml")
        {
            Directory.CreateDirectory(folderPath);
            File.WriteAllText(Path.Combine(folderPath, fileName), GetYaml(maxSize, maxTotalSize));
        }

        private static string GetYaml(long? maxSize, long? maxTotalSize)
        {
            var metadata = string.Empty;
            if (maxSize is not null)
                metadata += $"\n    - name: maxSize\n      value: \"{maxSize}\"";
            if (maxTotalSize is not null)
                metadata += $"\n    - name: maxTotalSize\n      value: \"{maxTotalSize}\"";

            return $"""
                    apiVersion: dapr.io/v1alpha1
                    kind: Component
                    metadata:
                      name: {Constants.DaprComponentNames.BinaryStoreComponentName}
                    spec:
                      type: binarystore.in-memory
                      version: v1
                      metadata:{(metadata.Length == 0 ? " []" : metadata)}
                    """;
        }
    }
}
