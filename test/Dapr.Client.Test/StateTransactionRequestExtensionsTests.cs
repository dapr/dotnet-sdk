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

public class StateTransactionRequestExtensionsTests
{
    [Fact]
    public void WithOutboxProjection_AddsProjectionMetadata_WithoutMutatingSource()
    {
        var source = new StateTransactionRequest("key1", new byte[] { 1 }, StateOperationType.Upsert);

        var projection = source.WithOutboxProjection();

        projection.ShouldNotBeSameAs(source);
        projection.Metadata.ShouldNotBeNull();
        projection.Metadata![DaprOutboxMetadata.Projection].ShouldBe(DaprOutboxMetadata.ProjectionEnabled);
        source.Metadata.ShouldBeNull();
    }

    [Fact]
    public void WithOutboxProjection_PreservesExistingMetadata()
    {
        var existing = new Dictionary<string, string> { ["ttlInSeconds"] = "60" };
        var source = new StateTransactionRequest("key1", new byte[] { 1 }, StateOperationType.Upsert, metadata: existing);

        var projection = source.WithOutboxProjection();

        projection.Metadata!["ttlInSeconds"].ShouldBe("60");
        projection.Metadata![DaprOutboxMetadata.Projection].ShouldBe(DaprOutboxMetadata.ProjectionEnabled);
    }

    [Fact]
    public void WithOutboxProjection_PreservesKeyValueEtagAndOptions()
    {
        var options = new StateOptions { Consistency = ConsistencyMode.Strong, Concurrency = ConcurrencyMode.FirstWrite };
        var source = new StateTransactionRequest(
            "key1",
            new byte[] { 1, 2, 3 },
            StateOperationType.Upsert,
            etag: "abc",
            options: options);

        var projection = source.WithOutboxProjection();

        projection.Key.ShouldBe(source.Key);
        projection.Value.ShouldBe(source.Value);
        projection.ETag.ShouldBe(source.ETag);
        projection.Options.ShouldBe(source.Options);
        projection.OperationType.ShouldBe(source.OperationType);
    }

    [Fact]
    public void WithOutboxProjection_ThrowsWhenRequestIsNull()
    {
        StateTransactionRequest source = null!;

        Should.Throw<ArgumentNullException>(() => source.WithOutboxProjection());
    }

    [Fact]
    public void WithCloudEventOverrides_MergesSuppliedFields()
    {
        var source = new StateTransactionRequest("key1", new byte[] { 1 }, StateOperationType.Upsert);

        var enriched = source.WithCloudEventOverrides(
            id: "evt-1",
            source: "orders",
            type: "OrderPlaced",
            subject: "order/42",
            dataContentType: "application/json");

        enriched.Metadata![DaprOutboxMetadata.CloudEventId].ShouldBe("evt-1");
        enriched.Metadata![DaprOutboxMetadata.CloudEventSource].ShouldBe("orders");
        enriched.Metadata![DaprOutboxMetadata.CloudEventType].ShouldBe("OrderPlaced");
        enriched.Metadata![DaprOutboxMetadata.CloudEventSubject].ShouldBe("order/42");
        enriched.Metadata![DaprOutboxMetadata.CloudEventDataContentType].ShouldBe("application/json");
    }

    [Fact]
    public void WithCloudEventOverrides_LeavesUnspecifiedFieldsAlone()
    {
        var source = new StateTransactionRequest("key1", new byte[] { 1 }, StateOperationType.Upsert);

        var enriched = source.WithCloudEventOverrides(id: "evt-1");

        enriched.Metadata!.ContainsKey(DaprOutboxMetadata.CloudEventId).ShouldBeTrue();
        enriched.Metadata!.ContainsKey(DaprOutboxMetadata.CloudEventSource).ShouldBeFalse();
        enriched.Metadata!.ContainsKey(DaprOutboxMetadata.CloudEventType).ShouldBeFalse();
    }

    [Fact]
    public void WithCloudEventOverrides_PreservesExistingMetadata()
    {
        var existing = new Dictionary<string, string> { ["ttlInSeconds"] = "60" };
        var source = new StateTransactionRequest("key1", new byte[] { 1 }, StateOperationType.Upsert, metadata: existing);

        var enriched = source.WithCloudEventOverrides(id: "evt-1");

        enriched.Metadata!["ttlInSeconds"].ShouldBe("60");
        enriched.Metadata![DaprOutboxMetadata.CloudEventId].ShouldBe("evt-1");
    }

    [Fact]
    public void WithCloudEventOverrides_DoesNotMutateSource()
    {
        var existing = new Dictionary<string, string> { ["ttlInSeconds"] = "60" };
        var source = new StateTransactionRequest("key1", new byte[] { 1 }, StateOperationType.Upsert, metadata: existing);

        _ = source.WithCloudEventOverrides(id: "evt-1");

        source.Metadata!.ContainsKey(DaprOutboxMetadata.CloudEventId).ShouldBeFalse();
        source.Metadata!.Count.ShouldBe(1);
    }

    [Fact]
    public void WithCloudEventOverrides_ThrowsWhenRequestIsNull()
    {
        StateTransactionRequest source = null!;

        Should.Throw<ArgumentNullException>(() => source.WithCloudEventOverrides(id: "evt-1"));
    }

    [Fact]
    public void WithCloudEventOverrides_WithNoValues_StillReturnsNewInstance()
    {
        var source = new StateTransactionRequest("key1", new byte[] { 1 }, StateOperationType.Upsert);

        var result = source.WithCloudEventOverrides();

        result.ShouldNotBeSameAs(source);
    }
}
