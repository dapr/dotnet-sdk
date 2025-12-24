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
using System.Threading.Tasks;
using Dapr.E2E.Test.Common;
using Dapr.TestContainers.Common.Options;
using Dapr.TestContainers.Harnesses;

namespace Dapr.TestContainers.Common;

/// <summary>
/// Builds the Dapr harnesses for different building blocks.
/// </summary>
/// <param name="options">The Dapr runtime options.</param>
/// <param name="startApp">The test app to run.</param>
public sealed class DaprHarnessBuilder(DaprRuntimeOptions options, Func<int, Task>? startApp = null)
{
    /// <summary>
    /// Builds a workflow harness.
    /// </summary>
    /// <param name="componentsDir">The path to the Dapr resources.</param>
	public WorkflowHarness BuildWorkflow(string componentsDir) => new(componentsDir, startApp, options);

    /// <summary>
    /// Builds a distributed lock harness.
    /// </summary>
    /// <param name="componentsDir">The path to the Dapr resources.</param>
	public DistributedLockHarness BuildDistributedLock(string componentsDir) => new(componentsDir, startApp, options);

    /// <summary>
    /// Builds a conversation harness.
    /// </summary>
    /// <param name="componentsDir">The path to the Dapr resources.</param>
	public ConversationHarness BuildConversation(string componentsDir) => new(componentsDir, startApp, options);

    /// <summary>
    /// Builds a cryptography harness.
    /// </summary>
    /// <param name="componentsDir">The path to the Dapr resources.</param>
    /// <param name="keysDir">The path to the cryptography keys.</param>
    public CryptographyHarness BuildCryptography(string componentsDir, string keysDir) =>
        new(componentsDir, startApp, keysDir, options);

    /// <summary>
    /// Builds a jobs harness.
    /// </summary>
    /// <param name="componentsDir">The path to the Dapr resources.</param>
	public JobsHarness BuildJobs(string componentsDir) => new(componentsDir, startApp, options);

    /// <summary>
    /// Builds a PubSub harness.
    /// </summary>
    /// <param name="componentsDir">The path to the Dapr resources.</param>
	public PubSubHarness BuildPubSub(string componentsDir) => new(componentsDir, startApp, options);

    /// <summary>
    /// Builds a state management harness.
    /// </summary>
    /// <param name="componentsDir">The path to the Dapr resources.</param>
	public StateManagementHarness BuildStateManagement(string componentsDir) => new(componentsDir, startApp, options);

    /// <summary>
    /// Builds an actor harness.
    /// </summary>
    /// <param name="componentsDir">The path to the Dapr resources.</param>
	public ActorHarness BuildActors(string componentsDir) => new(componentsDir, startApp, options);

    /// <summary>
    /// Creates a test application builder for the specified harness.
    /// </summary>
    public static DaprTestApplicationBuilder ForHarness(BaseHarness harness) => new(harness);
}
