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

using BenchmarkDotNet.Attributes;
using Dapr.Client;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Common.Testing;
using Dapr.Testcontainers.Harnesses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Benchmarks.StateManagement;

/// <summary>
/// Benchmarks for Dapr state management operations (save, get, delete) against
/// a real Dapr sidecar backed by a Redis state store via Testcontainers.
/// </summary>
[MemoryDiagnoser]
[MinIterationCount(3)]
[MaxIterationCount(10)]
[IterationCount(5)]
[WarmupCount(1)]
public class StateStoreBenchmarks : IDisposable
{
    private const string StoreName = "statestore";
    private DaprClient daprClient = null!;
    private DaprTestEnvironment? environment;
    private DaprTestApplication? testApp;
    private IServiceScope? scope;
    private bool disposed;

    /// <summary>
    /// The payload size category for the benchmark.
    /// </summary>
    [Params("Small", "Medium", "Large")]
    public string PayloadSize { get; set; } = "Small";

    private object payload = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory($"bench-state-{Guid.NewGuid():N}");

        environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true);
        await environment.StartAsync();

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildActors(); // Actors harness provides a Redis-backed state store

        testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(appBuilder =>
            {
                appBuilder.Services.AddDaprClient(configure: (sp, clientBuilder) =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                    var httpEndpoint = config["DAPR_HTTP_ENDPOINT"];
                    if (!string.IsNullOrEmpty(grpcEndpoint))
                        clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                    if (!string.IsNullOrEmpty(httpEndpoint))
                        clientBuilder.UseHttpEndpoint(httpEndpoint);
                });
            })
            .BuildAndStartAsync();

        scope = testApp.CreateScope();

        daprClient = scope.ServiceProvider.GetRequiredService<DaprClient>();

        payload = PayloadSize switch
        {
            "Small" => new { id = 1, name = "benchmark" },
            "Medium" => new
            {
                id = 1,
                name = "benchmark",
                description = new string('x', 1_000),
                tags = Enumerable.Range(0, 50).Select(i => $"tag-{i}").ToArray()
            },
            "Large" => new
            {
                id = 1,
                name = "benchmark",
                description = new string('x', 10_000),
                tags = Enumerable.Range(0, 500).Select(i => $"tag-{i}").ToArray(),
                nested = Enumerable.Range(0, 100).Select(i => new { index = i, value = new string('y', 100) }).ToArray()
            },
            _ => throw new ArgumentException($"Unknown payload size: {PayloadSize}")
        };
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        scope?.Dispose();
        scope = null;

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

        Cleanup().GetAwaiter().GetResult();

        GC.SuppressFinalize(this);
    }

    [Benchmark(Description = "SaveState")]
    public async Task SaveState()
    {
        var key = $"bench-{Guid.NewGuid():N}";
        await daprClient.SaveStateAsync(StoreName, key, payload);
    }

    [Benchmark(Description = "GetState")]
    public async Task GetState()
    {
        var key = $"bench-get-{PayloadSize}";
        // Ensure the key exists
        await daprClient.SaveStateAsync(StoreName, key, payload);
        await daprClient.GetStateAsync<object>(StoreName, key);
    }

    [Benchmark(Description = "DeleteState")]
    public async Task DeleteState()
    {
        var key = $"bench-del-{Guid.NewGuid():N}";
        await daprClient.SaveStateAsync(StoreName, key, payload);
        await daprClient.DeleteStateAsync(StoreName, key);
    }

    [Benchmark(Description = "SaveAndGetState")]
    public async Task SaveAndGetState()
    {
        var key = $"bench-sag-{Guid.NewGuid():N}";
        await daprClient.SaveStateAsync(StoreName, key, payload);
        await daprClient.GetStateAsync<object>(StoreName, key);
    }
}
