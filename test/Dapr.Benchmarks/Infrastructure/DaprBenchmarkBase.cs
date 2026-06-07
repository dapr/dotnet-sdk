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
// ------------------------------------------------------------------------

using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Common.Testing;
using Dapr.Testcontainers.Harnesses;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Benchmarks.Infrastructure;

/// <summary>
/// Base class that manages the lifecycle of a Dapr Testcontainers environment
/// for BenchmarkDotNet [GlobalSetup] / [GlobalCleanup].
/// </summary>
public abstract class DaprBenchmarkBase : IDisposable
{
    private DaprTestEnvironment? environment;
    private DaprTestApplication? testApp;
    private bool disposed;

    /// <summary>
    /// Gets the service scope created from the test application.
    /// </summary>
    protected IServiceScope? Scope { get; private set; }

    /// <summary>
    /// Creates the Dapr test environment and starts the application.
    /// </summary>
    protected async Task SetupEnvironmentAsync(
        Func<DaprHarnessBuilder, BaseHarness> buildHarness,
        Action<WebApplicationBuilder>? configureServices = null,
        Action<WebApplication>? configureApp = null,
        bool needsActorState = false)
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory($"bench-{Guid.NewGuid():N}");

        environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
            needsActorState: needsActorState);
        await environment.StartAsync();

        var harness = buildHarness(new DaprHarnessBuilder(componentsDir).WithEnvironment(environment));

        var appBuilder = DaprHarnessBuilder.ForHarness(harness);

        if (configureServices is not null)
        {
            appBuilder.ConfigureServices(configureServices);
        }

        if (configureApp is not null)
        {
            appBuilder.ConfigureApp(configureApp);
        }

        testApp = await appBuilder.BuildAndStartAsync();
        Scope = testApp.CreateScope();
    }

    /// <summary>
    /// Tears down the Dapr test environment and disposes resources.
    /// </summary>
    protected async Task TeardownEnvironmentAsync()
    {
        Scope?.Dispose();
        Scope = null;

        if (testApp is not null)
        {
            await testApp.DisposeAsync();
            testApp = null;
        }

        if (environment is not null)
        {
            await environment.DisposeAsync();
            environment = null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        TeardownEnvironmentAsync().GetAwaiter().GetResult();

        GC.SuppressFinalize(this);
    }
}
