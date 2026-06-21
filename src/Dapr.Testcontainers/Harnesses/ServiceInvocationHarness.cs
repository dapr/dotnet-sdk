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
using Dapr.Testcontainers.Common.Options;

namespace Dapr.Testcontainers.Harnesses;

/// <summary>
/// Provides a lightweight harness for Dapr service-invocation integration tests
/// (HTTP and gRPC proxy). No external infrastructure (Redis, RabbitMQ, …) is
/// required: the sidecar simply relays calls from the test to a local test app.
/// </summary>
public sealed class ServiceInvocationHarness : BaseHarness
{
    /// <summary>
    /// Initializes a new instance of <see cref="ServiceInvocationHarness"/>.
    /// </summary>
    /// <param name="componentsDir">The directory that will be mounted as the Dapr components path.</param>
    /// <param name="startApp">An optional delegate that starts a companion app on the given port.</param>
    /// <param name="options">Dapr runtime options (e.g. version, app-protocol).</param>
    /// <param name="environment">
    /// An optional shared <see cref="DaprTestEnvironment"/>.  When omitted the harness creates
    /// its own isolated environment (dedicated Docker network + Placement + Scheduler).
    /// </param>
    public ServiceInvocationHarness(
        string componentsDir,
        Func<int, Task>? startApp,
        DaprRuntimeOptions options,
        DaprTestEnvironment? environment = null)
        : base(componentsDir, startApp, options, environment)
    {
    }

    /// <inheritdoc />
    protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <inheritdoc />
    protected override ValueTask OnDisposeAsync()
        => ValueTask.CompletedTask;
}
