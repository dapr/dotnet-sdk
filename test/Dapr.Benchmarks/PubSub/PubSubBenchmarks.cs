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

using System.Collections.Concurrent;
using System.Text;
using BenchmarkDotNet.Attributes;
using Dapr.Benchmarks.Infrastructure;
using Dapr.Messaging.PublishSubscribe;
using Dapr.Testcontainers;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Harnesses;

namespace Dapr.Benchmarks.PubSub;

/// <summary>
/// Benchmarks for Dapr pub/sub operations (publish, subscribe, receive) against
/// a real Dapr sidecar backed by RabbitMQ via Testcontainers.
/// </summary>
[MemoryDiagnoser]
[MinIterationCount(3)]
[MaxIterationCount(10)]
[IterationCount(5)]
[WarmupCount(1)]
public class PubSubBenchmarks
{
    private const string PubSubName = Constants.DaprComponentNames.PubSubComponentName;
    private const string TopicName = "benchmark-topic";

    private BaseHarness? harness;
    private DaprPublishSubscribeClient? pubSubClient;
    private HttpClient? httpClient;

    /// <summary>
    /// The number of messages to publish per operation.
    /// </summary>
    [Params(1, 10)]
    public int MessageCount { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory($"bench-pubsub-{Guid.NewGuid():N}");

        harness = new DaprHarnessBuilder(componentsDir).BuildPubSub();
        await harness.InitializeAsync();

        pubSubClient = new DaprPublishSubscribeClientBuilder()
            .UseGrpcEndpoint($"http://127.0.0.1:{harness.DaprGrpcPort}")
            .UseHttpEndpoint($"http://127.0.0.1:{harness.DaprHttpPort}")
            .Build();

        httpClient = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{harness.DaprHttpPort}"),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        pubSubClient?.Dispose();
        httpClient?.Dispose();

        if (harness is not null)
        {
            await harness.DisposeAsync();
        }
    }

    [Benchmark(Description = "PublishMessages")]
    public async Task PublishMessages()
    {
        for (var i = 0; i < MessageCount; i++)
        {
            var payload = $"{{\"index\":{i},\"ts\":\"{DateTime.UtcNow:O}\"}}";
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await httpClient!.PostAsync(
                $"/v1.0/publish/{PubSubName}/{TopicName}",
                content);
            response.EnsureSuccessStatusCode();
        }
    }

    [Benchmark(Description = "PublishAndReceiveMessages")]
    public async Task PublishAndReceiveMessages()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var received = new ConcurrentBag<string>();
        using var allReceived = new SemaphoreSlim(0, MessageCount);

        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Success));

        // Each iteration uses a unique topic to avoid cross-iteration interference
        var topic = $"{TopicName}-{Guid.NewGuid():N}";

        await using var subscription = await pubSubClient!.SubscribeAsync(
            PubSubName,
            topic,
            options,
            (message, _) =>
            {
                received.Add(Encoding.UTF8.GetString(message.Data.Span));
                allReceived.Release();
                return Task.FromResult(TopicResponseAction.Success);
            },
            cts.Token);

        // Allow subscription to register with Dapr
        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);

        for (var i = 0; i < MessageCount; i++)
        {
            var payload = $"{{\"index\":{i}}}";
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await httpClient!.PostAsync(
                $"/v1.0/publish/{PubSubName}/{topic}",
                content,
                cts.Token);
            response.EnsureSuccessStatusCode();
        }

        for (var i = 0; i < MessageCount; i++)
        {
            await allReceived.WaitAsync(cts.Token);
        }
    }
}
