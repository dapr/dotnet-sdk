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

using System.Text.Json;
using Dapr.Testcontainers.Containers;

namespace Dapr.Testcontainers.Test.Common;

public sealed class MeilisearchContainerTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("a\"key\nwith: yaml", false)]
    [InlineData(null, true)]
    [InlineData("a\"key\nwith: yaml", true)]
    public void WritesComponentWithEscapedMetadata(string? apiKey, bool vector)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dapr-search-yaml-{Guid.NewGuid():N}");
        var file = Path.Combine(directory, "meilisearch-search.yaml");
        try
        {
            if (vector)
                MeilisearchContainer.Yaml.WriteVectorStoreYamlToFolder(directory, "http://meilisearch:7700", apiKey, Path.GetFileName(file));
            else
                MeilisearchContainer.Yaml.WriteSearchStoreYamlToFolder(directory, "http://meilisearch:7700", apiKey);
            var yaml = File.ReadAllText(file);
            Assert.Contains($"name: {(vector ? Constants.DaprComponentNames.VectorComponentName : Constants.DaprComponentNames.SearchComponentName)}", yaml);
            Assert.Contains($"type: {(vector ? "vector" : "search")}.meilisearch", yaml);
            Assert.Contains("version: v1", yaml);
            Assert.Contains("value: \"http://meilisearch:7700\"", yaml);
            if (apiKey is null)
                Assert.DoesNotContain("name: apiKey", yaml);
            else
                Assert.Contains($"value: {JsonSerializer.Serialize(apiKey)}", yaml);
        }
        finally
        {
            if (File.Exists(file))
                File.Delete(file);
            if (Directory.Exists(directory))
                Directory.Delete(directory);
        }
    }
}
