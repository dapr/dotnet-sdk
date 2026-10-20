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
using Dapr.Testcontainers.Containers;
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
    private DaprRuntimeOptions _options { get; set; } = new("1.17.0-rc.3");

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

    // /// <summary>
    // /// Sets an application to run as part of startup.
    // /// </summary>
    // /// <param name="startApp">The starting application.</param>
    // /// <returns></returns>
    // public DaprHarnessBuilder WithStartUp(Func<int, Task> startApp)
    // {
    //     this._startApp = startApp;
    //     return this;
    // }

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

    /// <summary>
    /// Builds a distributed lock harness.
    /// </summary>
    public DistributedLockHarness BuildDistributedLock() =>
        new(_componentsDirectory, _startApp, _options, _environment);

    /// <summary>
    /// Builds a secret store harness.
    /// </summary>
    public SecretStoreHarness BuildSecretStore() => new(_componentsDirectory, _startApp, _options, _environment);

    /// <summary>Builds a Search harness backed by Meilisearch. Select a Search-capable runtime with WithOptions.</summary>
    public SearchHarness BuildSearch(string meilisearchImage = MeilisearchContainer.DefaultImage) =>
        new(_componentsDirectory, _startApp, _options, _environment, meilisearchImage);

    /// <summary>Builds a Vector harness backed by Meilisearch. Select a Vector-capable runtime with WithOptions.</summary>
    public VectorHarness BuildVector(string meilisearchImage = MeilisearchContainer.DefaultImage) =>
        new(_componentsDirectory, _startApp, _options, _environment, meilisearchImage);

    /// <summary>
    /// Builds a conversation harness.
    /// </summary>
    public ConversationHarness BuildConversation() => new(_componentsDirectory, _startApp, _options, _environment);

    /// <summary>
    /// Builds a cryptography harness.
    /// </summary>
    /// <param name="keysDir">The path to the cryptography keys.</param>
    public CryptographyHarness BuildCryptography(string keysDir) =>
        new(_componentsDirectory, _startApp, keysDir, _options, _environment);

    /// <summary>
    /// Builds a binary store harness.
    /// </summary>
    /// <param name="maxSize">Optional maximum size in bytes of a single file.</param>
    /// <param name="maxTotalSize">Optional maximum total size in bytes of the store.</param>
    public BinaryStoreHarness BuildBinaryStore(long? maxSize = null, long? maxTotalSize = null) =>
        new(_componentsDirectory, _startApp, _options, maxSize, maxTotalSize, _environment);

    /// <summary>
    /// Builds a jobs harness.
    /// </summary>
    public JobsHarness BuildJobs() => new(_componentsDirectory, _startApp, _options, _environment);

    /// <summary>
    /// Builds a Metadata harness.
    /// </summary>
    public MetadataHarness BuildMetadata() => new(_componentsDirectory, _startApp, _options, _environment);

    /// <summary>
    /// Builds a PubSub harness.
    /// </summary>
    public PubSubHarness BuildPubSub() => new(_componentsDirectory, _startApp, _options, _environment);

    /// <summary>
    /// Builds an actor harness.
    /// </summary>
    public ActorHarness BuildActors() => new(_componentsDirectory, _startApp, _options, _environment);

    /// <summary>
    /// Builds a service invocation harness.
    /// </summary>
    public ServiceInvocationHarness BuildServiceInvocation() =>
        new(_componentsDirectory, _startApp, _options, _environment);

    /// <summary>
    /// Creates a test application builder for the specified harness.
    /// </summary>
    public static DaprTestApplicationBuilder ForHarness(BaseHarness harness) => new(harness);
}
