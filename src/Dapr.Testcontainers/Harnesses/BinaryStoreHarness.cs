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

using System;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Containers;

namespace Dapr.Testcontainers.Harnesses;

/// <summary>
/// Provides an implementation harness for Dapr's binary store building block.
/// </summary>
/// <param name="componentsDir">The directory to Dapr components.</param>
/// <param name="startApp">The test app to validate in the harness.</param>
/// <param name="options">The Dapr runtime options.</param>
/// <param name="maxSize">Optional maximum size in bytes of a single file.</param>
/// <param name="maxTotalSize">Optional maximum total size in bytes of the store.</param>
/// <param name="environment">The isolated environment instance.</param>
public sealed class BinaryStoreHarness(string componentsDir, Func<int, Task>? startApp, DaprRuntimeOptions options, long? maxSize = null, long? maxTotalSize = null, DaprTestEnvironment? environment = null) : BaseHarness(componentsDir, startApp, options, environment)
{
    private readonly string componentsDir = componentsDir;
    private readonly long? maxSize = maxSize;
    private readonly long? maxTotalSize = maxTotalSize;

    /// <inheritdoc />
    protected override Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        InMemoryBinaryStoreContainer.Yaml.WriteBinaryStoreYamlToFolder(componentsDir, maxSize, maxTotalSize);
        return Task.CompletedTask;
    }
}
