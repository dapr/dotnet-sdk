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
using Moq;

namespace Dapr.Messaging.Examples.Publishing.Tests;

public sealed class PublishingTests
{
    [Fact]
    public async Task Publish_standard_event_invokes_client_with_correct_arguments()
    {
        var mockClient = new Mock<IDaprPublishSubscribeClient>();
        var order = new OrderPlaced("ord-123", "cust-1", 49.99m, ["item-1"]);

        mockClient
            .Setup(c => c.PublishEventAsync("pubsub", "orders", order, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        await mockClient.Object.PublishEventAsync("pubsub", "orders", order, CancellationToken.None);

        mockClient.Verify();
    }

    [Fact]
    public async Task Publish_with_options_includes_custom_metadata()
    {
        var mockClient = new Mock<IDaprPublishSubscribeClient>();
        var priorityOrder = new PriorityOrder("ord-999", "cust-vip", 999.00m, "Platinum");
        var options = new PublishOptions
        {
            Metadata =
            {
                ["cloudevent.type"] = "priority.order",
                ["ttlInSeconds"] = "60"
            }
        };

        mockClient
            .Setup(c => c.PublishEventAsync(
                "pubsub",
                "orders",
                priorityOrder,
                It.Is<PublishOptions>(o => o.Metadata["cloudevent.type"] == "priority.order" && o.Metadata["ttlInSeconds"] == "60"),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        await mockClient.Object.PublishEventAsync("pubsub", "orders", priorityOrder, options, CancellationToken.None);

        mockClient.Verify();
    }

    [Fact]
    public async Task Bulk_publish_events_returns_response_with_failed_entries()
    {
        var mockClient = new Mock<IDaprPublishSubscribeClient>();
        var batch = new List<OrderBatchItem>
        {
            new("ord-1", 10.0m),
            new("ord-2", 20.0m)
        };

        var expectedResponse = new BulkPublishResponse<OrderBatchItem>(
            failedEntries: new List<BulkPublishResponseFailedEntry<OrderBatchItem>>());

        mockClient
            .Setup(c => c.BulkPublishEventAsync("pubsub", "orders", batch, It.IsAny<PublishOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse)
            .Verifiable();

        var result = await mockClient.Object.BulkPublishEventAsync("pubsub", "orders", batch);

        Assert.NotNull(result);
        Assert.Empty(result.FailedEntries);
        mockClient.Verify();
    }

    [Fact]
    public async Task Publish_raw_bytes_forwards_content_type()
    {
        var mockClient = new Mock<IDaprPublishSubscribeClient>();
        var data = Encoding.UTF8.GetBytes("RAW_PAYLOAD");

        mockClient
            .Setup(c => c.PublishByteEventAsync(
                "pubsub",
                "raw-orders",
                It.IsAny<ReadOnlyMemory<byte>>(),
                "text/plain",
                It.IsAny<PublishOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        await mockClient.Object.PublishByteEventAsync(
            "pubsub",
            "raw-orders",
            data,
            "text/plain");

        mockClient.Verify();
    }
}
