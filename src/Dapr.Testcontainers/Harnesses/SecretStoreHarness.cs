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
using System.Threading;
using System.Threading.Tasks;
using Dapr.Testcontainers.Common.Options;

namespace Dapr.Testcontainers.Harnesses;

/// <summary>
/// Provides an implementation harness for Dapr's secret store building block using a local file-based secret store.
/// </summary>
public sealed class SecretStoreHarness : BaseHarness
{
    private readonly string componentsDir;

    /// <summary>
    /// The name of the secret store component.
    /// </summary>
    public const string SecretStoreComponentName = "localsecretstore";

    /// <summary>
    /// Provides an implementation harness for Dapr's secret store building block.
    /// </summary>
    /// <param name="componentsDir">The directory to Dapr components.</param>
    /// <param name="startApp">The test app to validate in the harness.</param>
    /// <param name="options">The Dapr runtime options.</param>
    /// <param name="environment">The isolated environment instance.</param>
    public SecretStoreHarness(string componentsDir, System.Func<int, Task>? startApp, DaprRuntimeOptions options, DaprTestEnvironment? environment = null) : base(componentsDir, startApp, options, environment)
    {
        this.componentsDir = componentsDir;
    }

    /// <inheritdoc />
    protected override Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        WriteSecretsFile(componentsDir);
        WriteComponentYaml(componentsDir);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes the secrets JSON file to the specified directory.
    /// </summary>
    public static void WriteSecretsFile(string folderPath, string fileName = "secrets.json")
    {
        Directory.CreateDirectory(folderPath);
        var fullPath = Path.Combine(folderPath, fileName);
        File.WriteAllText(fullPath, @"{
  ""secret1"": ""value1"",
  ""secret2"": ""value2""
}");
    }

    /// <summary>
    /// Writes the component YAML file to the specified directory.
    /// </summary>
    public static void WriteComponentYaml(string folderPath, string fileName = "secretstore.yaml")
    {
        Directory.CreateDirectory(folderPath);
        var fullPath = Path.Combine(folderPath, fileName);
        File.WriteAllText(fullPath, $@"apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: {SecretStoreComponentName}
  namespace: default
spec:
  type: secretstores.local.file
  version: v1
  metadata:
  - name: secretsFile
    value: /components/secrets.json
  - name: nestedSeparator
    value: "":""
");
    }
}
