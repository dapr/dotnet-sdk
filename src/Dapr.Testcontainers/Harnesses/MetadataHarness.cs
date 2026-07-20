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
//  ------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Testcontainers.Common.Options;

namespace Dapr.Testcontainers.Harnesses;

/// <summary>
/// Provides an implementation harness for the Metadata building block.
/// </summary>
public sealed class MetadataHarness(
    string componentsDir,
    Func<int, Task>? startApp,
    DaprRuntimeOptions options,
    DaprTestEnvironment? environment = null)
    : BaseHarness(componentsDir, startApp, options, environment)
{
    /// <inheritdoc />
    protected override Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        DaprPlacementExternalPort = Environment.PlacementExternalPort;
        DaprPlacementAlias = Environment.PlacementAlias;
        DaprSchedulerExternalPort = Environment.SchedulerExternalPort;
        DaprSchedulerAlias = Environment.SchedulerAlias;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override ValueTask OnDisposeAsync() => ValueTask.CompletedTask;
}
