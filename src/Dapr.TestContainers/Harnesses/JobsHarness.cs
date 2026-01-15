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

using System;
using System.Threading;
using System.Threading.Tasks;
using Dapr.TestContainers.Common.Options;

namespace Dapr.TestContainers.Harnesses;

/// <summary>
/// Provides an implementation harness for the Jobs building block.
/// </summary>
public sealed class JobsHarness : BaseHarness
{
    /// <summary>
    /// Provides an implementation harness for the Jobs building block.
    /// </summary>
    /// <param name="componentsDir">The directory to Dapr components.</param>
    /// <param name="startApp">The test app to validate in the harness.</param>
    /// <param name="options">The Dapr runtime options.</param>
    /// <param name="environment">The isolated environment instance.</param>
    public JobsHarness(string componentsDir, Func<int, Task>? startApp, DaprRuntimeOptions options, DaprTestEnvironment? environment = null) : base(componentsDir, startApp, options, environment)
    {
    }

    /// <inheritdoc />
	protected override Task OnInitializeAsync(CancellationToken cancellationToken)
	{
        // Set the service ports
        this.DaprPlacementExternalPort = Environment.PlacementExternalPort;
        this.DaprPlacementAlias = Environment.PlacementAlias;
        this.DaprSchedulerExternalPort = Environment.SchedulerExternalPort;
        this.DaprSchedulerAlias = Environment.SchedulerAlias;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override ValueTask OnDisposeAsync() => ValueTask.CompletedTask;
}
