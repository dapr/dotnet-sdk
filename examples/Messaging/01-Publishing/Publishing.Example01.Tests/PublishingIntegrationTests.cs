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
// ------------------------------------------------------------------------

using System.Text;
using Dapr.Messaging;
using Dapr.Messaging.Examples.Publishing;
using Dapr.Messaging.PublishSubscribe;
using Dapr.Testcontainers;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Harnesses;

namespace Dapr.Messaging.Examples.Publishing.Tests;

/// <summary>
/// Demonstrates how to write end-to-end integration tests for Dapr message publishing
/// against a real Dapr sidecar and Redis message broker using <c>Dapr.Testcontainers</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Integration Testing Pattern:</b>
/// </para>
/// <list type="number">
///   <item>
///     <description>
///       <b>Initialize Testcontainers:</b> In <see cref="IAsyncLifetime.InitializeAsync"/>, use
///       <see cref="DaprHarnessBuilder"/> to create a <see cref="PubSubHarness"/>. This starts
///       a containerized Redis instance and a <c>daprd</c> sidecar with a configured <c>pubsub</c> component.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Configure Client:</b> Build a <see cref="DaprPublishSubscribeClient"/> pointing to the
///       dynamically assigned ports (<see cref="BaseHarness.DaprGrpcPort"/> and <see cref="BaseHarness.DaprHttpPort"/>).
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Execute and Assert:</b> Publish real events (strongly-typed, bulk, raw bytes) and verify
///       that Dapr accepts the payload without errors.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Cleanup:</b> Dispose the client and harness in <see cref="IAsyncLifetime.DisposeAsync"/>.
///     </description>
///   </item>
/// </list>
/// </remarks>
public sealed class PublishingIntegrationTests : IAsyncLifetime
{
    private const string PubSubName = Constants.DaprComponentNames.PubSubComponentName;
    private const string OrdersTopic = "integration-orders-topic";

    private BaseHarness? _harness;
    private DaprPublishSubscribeClient? _client;

    /// <summary>
    /// Starts the real Dapr sidecar and Redis pub/sub broker via Testcontainers.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("pubsub-publishing-example");

        // DaprHarnessBuilder creates a PubSubHarness containing a Redis container and daprd container
        _harness = new DaprHarnessBuilder(componentsDir)
            .WithOptions(new DaprRuntimeOptions())
            .BuildPubSub();

        await _harness.InitializeAsync();

        // Connect the typed Dapr messaging client to the dynamically assigned container ports
        _client = new DaprPublishSubscribeClientBuilder()
            .UseGrpcEndpoint($"http://127.0.0.1:{_harness.DaprGrpcPort}")
            .UseHttpEndpoint($"http://127.0.0.1:{_harness.DaprHttpPort}")
            .Build();
    }

    /// <summary>
    /// Tears down container resources after the test suite completes.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_harness is not null)
        {
            await _harness.DisposeAsync();
        }
    }

    [Fact]
    public async Task PublishEventAsync_WithTypedPayload_PublishesSuccessfullyToRealDapr()
    {
        Assert.NotNull(_client);

        var order = new OrderPlaced("ord-live-100", "customer-42", 149.99m, ["item-x", "item-y"]);

        // Act & Assert: Publishing to real Dapr runtime should complete successfully
        await _client.PublishEventAsync(
            PubSubName,
            OrdersTopic,
            order,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PublishEventAsync_WithOptionsAndMetadata_PublishesSuccessfullyToRealDapr()
    {
        Assert.NotNull(_client);

        var priorityOrder = new PriorityOrder("ord-live-vip", "cust-vip", 899.00m, "Gold");
        var options = new PublishOptions
        {
            Metadata =
            {
                ["cloudevent.type"] = "orders.priority",
                ["ttlInSeconds"] = "120"
            }
        };

        await _client.PublishEventAsync(
            PubSubName,
            OrdersTopic,
            priorityOrder,
            options,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task BulkPublishEventAsync_WithBatch_PublishesAllEventsSuccessfully()
    {
        Assert.NotNull(_client);

        var batch = new List<OrderBatchItem>
        {
            new("batch-item-1", 25.00m),
            new("batch-item-2", 50.00m),
            new("batch-item-3", 75.00m)
        };

        var response = await _client.BulkPublishEventAsync(
            PubSubName,
            OrdersTopic,
            batch,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.Empty(response.FailedEntries);
    }

    [Fact]
    public async Task PublishByteEventAsync_WithRawPayload_PublishesSuccessfullyToRealDapr()
    {
        Assert.NotNull(_client);

        var payload = Encoding.UTF8.GetBytes("{\"custom\":\"raw-event-data\"}");

        await _client.PublishByteEventAsync(
            PubSubName,
            OrdersTopic,
            payload,
            "application/json",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
