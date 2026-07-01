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

namespace Dapr.Client.Test;

using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;

public class OutboxTransactionBuilderTests
{
    [Fact]
    public void Build_WithUpsertOnly_ReturnsSingleRequest()
    {
        var result = new OutboxTransactionBuilder()
            .Upsert("key1", new byte[] { 1, 2, 3 })
            .Build();

        result.Count.ShouldBe(1);
        result[0].Key.ShouldBe("key1");
        result[0].OperationType.ShouldBe(StateOperationType.Upsert);
        result[0].Metadata.ShouldBeNull();
    }

    [Fact]
    public void Build_WithUpsertWithProjection_EmitsPairedRequests_InCorrectOrder()
    {
        var result = new OutboxTransactionBuilder()
            .UpsertWithProjection(
                key: "key1",
                stateValue: new byte[] { 1 },
                projectionValue: new byte[] { 2 })
            .Build();

        result.Count.ShouldBe(2);

        // State operation first
        result[0].Key.ShouldBe("key1");
        result[0].Value.ShouldBe(new byte[] { 1 });
        result[0].Metadata.ShouldBeNull();

        // Projection second, with outbox.projection = true
        result[1].Key.ShouldBe("key1");
        result[1].Value.ShouldBe(new byte[] { 2 });
        result[1].Metadata.ShouldNotBeNull();
        result[1].Metadata![DaprOutboxMetadata.Projection].ShouldBe(DaprOutboxMetadata.ProjectionEnabled);
    }

    [Fact]
    public void Build_WithProjectionMetadata_MergesCloudEventOverrides()
    {
        var projectionMeta = new Dictionary<string, string>
        {
            [DaprOutboxMetadata.CloudEventType] = "OrderPlaced",
            [DaprOutboxMetadata.CloudEventSource] = "orders",
        };

        var result = new OutboxTransactionBuilder()
            .UpsertWithProjection(
                key: "order-42",
                stateValue: new byte[] { 1 },
                projectionValue: new byte[] { 2 },
                projectionMetadata: projectionMeta)
            .Build();

        var projection = result[1];
        projection.Metadata![DaprOutboxMetadata.Projection].ShouldBe(DaprOutboxMetadata.ProjectionEnabled);
        projection.Metadata![DaprOutboxMetadata.CloudEventType].ShouldBe("OrderPlaced");
        projection.Metadata![DaprOutboxMetadata.CloudEventSource].ShouldBe("orders");
    }

    [Fact]
    public void Build_WithDelete_EmitsDeleteRequest()
    {
        var result = new OutboxTransactionBuilder()
            .Delete("key1", etag: "abc")
            .Build();

        result.Count.ShouldBe(1);
        result[0].Key.ShouldBe("key1");
        result[0].OperationType.ShouldBe(StateOperationType.Delete);
        result[0].Value.ShouldBeNull();
        result[0].ETag.ShouldBe("abc");
    }

    [Fact]
    public void Build_WithMultipleEntries_PreservesOrder()
    {
        var result = new OutboxTransactionBuilder()
            .Upsert("a", new byte[] { 1 })
            .UpsertWithProjection("b", new byte[] { 2 }, new byte[] { 3 })
            .Delete("c")
            .Build();

        result.Count.ShouldBe(4);
        result[0].Key.ShouldBe("a");
        result[1].Key.ShouldBe("b");
        result[1].Metadata.ShouldBeNull();
        result[2].Key.ShouldBe("b");
        result[2].Metadata![DaprOutboxMetadata.Projection].ShouldBe(DaprOutboxMetadata.ProjectionEnabled);
        result[3].Key.ShouldBe("c");
        result[3].OperationType.ShouldBe(StateOperationType.Delete);
    }

    [Fact]
    public void Upsert_ThrowsOnNullOrEmptyKey()
    {
        var builder = new OutboxTransactionBuilder();
        Should.Throw<ArgumentException>(() => builder.Upsert("", new byte[] { 1 }));
        Should.Throw<ArgumentNullException>(() => builder.Upsert(null!, new byte[] { 1 }));
    }

    [Fact]
    public void Upsert_ThrowsOnNullValue()
    {
        var builder = new OutboxTransactionBuilder();
        Should.Throw<ArgumentNullException>(() => builder.Upsert("k", null!));
    }

    [Fact]
    public void UpsertWithProjection_ThrowsOnNullOrEmptyKey()
    {
        var builder = new OutboxTransactionBuilder();
        Should.Throw<ArgumentException>(() => builder.UpsertWithProjection("", new byte[] { 1 }, new byte[] { 2 }));
    }

    [Fact]
    public void UpsertWithProjection_ThrowsOnNullValues()
    {
        var builder = new OutboxTransactionBuilder();
        Should.Throw<ArgumentNullException>(() => builder.UpsertWithProjection("k", null!, new byte[] { 2 }));
        Should.Throw<ArgumentNullException>(() => builder.UpsertWithProjection("k", new byte[] { 1 }, null!));
    }

    [Fact]
    public void Delete_ThrowsOnNullOrEmptyKey()
    {
        var builder = new OutboxTransactionBuilder();
        Should.Throw<ArgumentException>(() => builder.Delete(""));
        Should.Throw<ArgumentNullException>(() => builder.Delete(null!));
    }

    [Fact]
    public void Build_OnEmptyBuilder_ReturnsEmptyList()
    {
        new OutboxTransactionBuilder().Build().Count.ShouldBe(0);
    }
}
