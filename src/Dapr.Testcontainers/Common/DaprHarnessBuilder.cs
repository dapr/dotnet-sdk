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
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Common.Testing;
using Dapr.Testcontainers.Harnesses;

namespace Dapr.Testcontainers.Common;

/// <summary>
/// Builds the Dapr harnesses for different building blocks.
/// </summary>
public sealed class DaprHarnessBuilder
{
    /// <summary>
    /// The Dapr container runtime options.
    /// </summary>
    private DaprRuntimeOptions _options { get; set; } = new();
    /// <summary>
    /// The isolated test environment to use with the harness, if any.
    /// </summary>
    private DaprTestEnvironment? _environment { get; set; }
    /// <summary>
    /// The directory containing the Dapr component resources.
    /// </summary>
    private string _componentsDirectory { get; set; }
    /// <summary>
    /// An application to run at startup.
    /// </summary>
    private Func<int, Task>? _startApp { get; set; }

    /// <summary>
    /// Builds the Dapr harnesses for different building blocks.
    /// </summary>
    /// <param name="componentsDirectory">The path to the Dapr component resources.</param>
    public DaprHarnessBuilder(string componentsDirectory)
    {
        _componentsDirectory = componentsDirectory;
    }
    
    /// <summary>
    /// Sets the <see cref="DaprRuntimeOptions"/> on the builder.
    /// </summary>
    /// <param name="options">Options for configuring the Dapr container runtime.</param>
    /// <returns>This instance of the <see cref="DaprHarnessBuilder"/>.</returns>
    public DaprHarnessBuilder WithOptions(DaprRuntimeOptions options)
    {
        this._options = options;
        return this;
    }

    /// <summary>
    /// Sets an application to run as part of startup.
    /// </summary>
    /// <param name="startApp">The starting application.</param>
    /// <returns></returns>
    public DaprHarnessBuilder WithStartUp(Func<int, Task> startApp)
    {
        this._startApp = startApp;
        return this;
    }

    /// <summary>
    /// Sets the shared test environment.
    /// </summary>
    /// <param name="environment">The test environment to be used by the harness.</param>
    /// <returns>This instance of the <see cref="DaprHarnessBuilder"/>.</returns>
    public DaprHarnessBuilder WithEnvironment(DaprTestEnvironment environment)
    {
        this._environment = environment;
        return this;
    }
    
    /// <summary>
    /// Builds a workflow harness.
    /// </summary>
	public WorkflowHarness BuildWorkflow() => new(_componentsDirectory, _startApp, _options, _environment);

 //    /// <summary>
 //    /// Builds a distributed lock harness.
 //    /// </summary>
	// public DistributedLockHarness BuildDistributedLock() => new(_componentsDirectory, _startApp, _options, _environment);
 //
 //    /// <summary>
 //    /// Builds a conversation harness.
 //    /// </summary>
	// public ConversationHarness BuildConversation() => new(_componentsDirectory, _startApp, _options, _environment);
 //
 //    /// <summary>
 //    /// Builds a cryptography harness.
 //    /// </summary>
 //    /// <param name="keysDir">The path to the cryptography keys.</param>
 //    public CryptographyHarness BuildCryptography(string keysDir) =>
 //        new(_componentsDirectory, _startApp, keysDir, _options, _environment);

    /// <summary>
    /// Builds a jobs harness.
    /// </summary>
	public JobsHarness BuildJobs() => new(_componentsDirectory, _startApp, _options, _environment);

 //    /// <summary>
 //    /// Builds a PubSub harness.
 //    /// </summary>
 //    /// <param name="componentsDir">The path to the Dapr resources.</param>
	// public PubSubHarness BuildPubSub(string componentsDir) => new(_componentsDirectory, _startApp, _options, _environment);
 //
 //    /// <summary>
 //    /// Builds a state management harness.
 //    /// </summary>
 //    /// <param name="componentsDir">The path to the Dapr resources.</param>
	// public StateManagementHarness BuildStateManagement(string componentsDir) => new(_componentsDirectory, _startApp, _options, _environment);
 //
 //    /// <summary>
 //    /// Builds an actor harness.
 //    /// </summary>
 //    /// <param name="componentsDir">The path to the Dapr resources.</param>
	// public ActorHarness BuildActors(string componentsDir) => new(_componentsDirectory, _startApp, _options, _environment);

    /// <summary>
    /// Creates a test application builder for the specified harness.
    /// </summary>
    public static DaprTestApplicationBuilder ForHarness(BaseHarness harness) => new(harness);
}
