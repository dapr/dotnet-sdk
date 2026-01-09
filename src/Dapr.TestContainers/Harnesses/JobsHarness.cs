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
using Dapr.TestContainers.Containers.Dapr;

namespace Dapr.TestContainers.Harnesses;

/// <summary>
/// Provides an implementation harness for the Jobs building block.
/// </summary>
public sealed class JobsHarness : BaseHarness
{
	private readonly DaprSchedulerContainer _scheduler;
    private readonly string componentsDir;

    /// <summary>
    /// Provides an implementation harness for the Jobs building block.
    /// </summary>
    /// <param name="componentsDir">The directory to Dapr components.</param>
    /// <param name="startApp">The test app to validate in the harness.</param>
    /// <param name="options">The Dapr runtime options.</param>
    /// <param name="environment">The isolated environment instance.</param>
    public JobsHarness(string componentsDir, Func<int, Task>? startApp, DaprRuntimeOptions options, DaprTestEnvironment? environment = null) : base(componentsDir, startApp, options, environment)
    {
        this.componentsDir = componentsDir;
        _scheduler = new DaprSchedulerContainer(options, Network);
    }

    /// <inheritdoc />
	protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
	{
        // Start the infrastructure
        await _scheduler.StartAsync(cancellationToken);
        DaprSchedulerExternalPort = _scheduler.ExternalPort;
        DaprSchedulerAlias = _scheduler.NetworkAlias;
    }
	
    /// <inheritdoc />
	protected override async ValueTask OnDisposeAsync()
    {
        await _scheduler.DisposeAsync();
	}
}
