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

using System.Text.Json;
using Dapr.Common;
using Dapr.Vector;
using Dapr.Testcontainers;
using Grpc.Core;

namespace Dapr.IntegrationTest.Vector;

public sealed class VectorTests(VectorFixture fixture) : IClassFixture<VectorFixture>
{
    private const string Store = Constants.DaprComponentNames.VectorComponentName;
    private DaprVectorClient Client => fixture.Client;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly IndexingOptions Wait = new()
    {
        Mode = IndexingMode.WaitForCompletion,
        WaitTimeout = TimeSpan.FromSeconds(20),
        OnWaitTimeout = IndexingWaitTimeoutAction.FailRequest
    };

    [VectorFact]
    public async Task GeneratedCollectionCompletesTypedLifecycle()
    {
        var collection = fixture.ProductVector;
        await collection.CreateCollectionAsync(3, DistanceMetric.Cosine, cancellationToken: Ct);
        try
        {
            var product = new VectorProduct("Dapr", 25);
            var write = await collection.UpsertVectorsAsync([new("1", [1, 0, 0], product)], Wait,
                cancellationToken: Ct);
            Assert.Equal(IndexAck.Completed, write.Ack);
            Assert.Empty(write.FailedItems);
            var info = await collection.GetCollectionAsync(cancellationToken: Ct);
            Assert.Equal(("generated-products", 3U, DistanceMetric.Cosine),
                (info.Collection, info.Dimensions, info.Metric));
            Assert.Equal(product,
                Assert.Single(await collection.GetVectorsAsync(["1"], true, cancellationToken: Ct)).Payload);
            var query = new VectorQuery { Vector = [1, 0, 0], IncludePayload = true, IncludeValues = true, TopK = 1 };
            var result = await collection.QueryVectorsAsync(query, cancellationToken: Ct);
            Assert.Equal(product, Assert.Single(result.Matches).Record.Payload);
            var batch = Assert.Single(await collection.BatchQueryVectorsAsync([query], cancellationToken: Ct));
            Assert.Null(batch.Error);
            Assert.Equal(product, Assert.Single(batch.Response!.Matches).Record.Payload);
            Assert.Equal(IndexAck.Completed, await collection.DeleteVectorsAsync(["1"], Wait, cancellationToken: Ct));
            Assert.Empty(await collection.GetVectorsAsync(["1"], cancellationToken: Ct));
        }
        finally
        {
            await collection.DeleteCollectionAsync(cancellationToken: Ct);
        }

        Assert.DoesNotContain("generated-products", await Client.ListCollectionsAsync(Store, cancellationToken: Ct));
    }

    [VectorFact]
    public async Task CollectionAndOpaqueRecordLifecycleRoundTrips()
    {
        var collection = NewCollection();
        await Client.CreateCollectionAsync(Store, collection, 3, cancellationToken: Ct);
        try
        {
            Assert.Contains(collection, await Client.ListCollectionsAsync(Store, cancellationToken: Ct));
            var info = await Client.GetCollectionAsync(Store, collection, cancellationToken: Ct);
            Assert.Equal((collection, 0UL, 3U, DistanceMetric.Cosine),
                (info.Collection, info.RecordCount, info.Dimensions, info.Metric));
            byte[] payload = [0, 255, 128, 1];
            var metadata =
                JsonSerializer.Deserialize<JsonElement>("""{"category":"books","nested":{"available":true}}""");
            var write = await Client.UpsertVectorsAsync<byte[]>(Store, collection,
                [new("first", [1, 0, 0], payload, metadata), new("second", [0, 1, 0])], Wait, cancellationToken: Ct);
            Assert.Equal(IndexAck.Completed, write.Ack);
            Assert.Empty(write.FailedItems);
            var records = await Client.GetVectorsAsync<byte[]>(Store, collection, ["second", "missing", "first"], true,
                cancellationToken: Ct);
            Assert.Equal(["second", "first"], records.Select(record => record.Id));
            Assert.Equal(payload, records[1].Payload);
            Assert.Equal([1f, 0f, 0f], records[1].Values);
            Assert.True(records[1].Metadata!.Value.GetProperty("nested").GetProperty("available").GetBoolean());
            var noValues =
                Assert.Single(
                    await Client.GetVectorsAsync<byte[]>(Store, collection, ["first"], cancellationToken: Ct));
            Assert.Empty(noValues.Values);
            Assert.Equal(payload, noValues.Payload);
            Assert.Equal(2UL, (await Client.GetCollectionAsync(Store, collection, cancellationToken: Ct)).RecordCount);
            await Client.UpsertVectorsAsync<byte[]>(Store, collection, [new("first", [0, 0, 1], [42])], Wait,
                cancellationToken: Ct);
            Assert.Equal(new byte[] { 42 },
                Assert.Single(await Client.GetVectorsAsync<byte[]>(Store, collection, ["first"], cancellationToken: Ct))
                    .Payload);
            Assert.Equal(IndexAck.Completed,
                await Client.DeleteVectorsAsync(Store, collection, ["first", "missing"], Wait, cancellationToken: Ct));
            Assert.Empty(await Client.GetVectorsAsync<byte[]>(Store, collection, ["first"], cancellationToken: Ct));
        }
        finally
        {
            await Client.DeleteCollectionAsync(Store, collection, cancellationToken: Ct);
        }

        Assert.DoesNotContain(collection, await Client.ListCollectionsAsync(Store, cancellationToken: Ct));
    }

    [VectorFact]
    public async Task QueriesFilterShapeApplyThresholdAndPreserveBatchErrors()
    {
        var collection = NewCollection();
        await Client.CreateCollectionAsync(Store, collection, 3, DistanceMetric.Cosine, cancellationToken: Ct);
        try
        {
            var write = await Client.UpsertVectorsAsync<string>(Store, collection,
            [
                new("closest", [1, 0, 0], "first", Attributes("books")),
                new("orthogonal", [0, 1, 0], "second", Attributes("books")),
                new("other", [0.8f, 0.6f, 0], "third", Attributes("films"))
            ], Wait, cancellationToken: Ct);
            Assert.Empty(write.FailedItems);
            var query = new VectorQuery
            {
                Vector = [1, 0, 0],
                TopK = 3,
                IncludeValues = true,
                IncludePayload = true,
                Filter = JsonSerializer.Deserialize<JsonElement>("""{"category":{"$eq":"books"}}""")
            };
            var result = await Client.QueryVectorsAsync<string>(Store, collection, query, cancellationToken: Ct);
            Assert.Equal(DistanceMetric.Cosine, result.Metric);
            Assert.Equal(["closest", "orthogonal"], result.Matches.Select(match => match.Record.Id));
            Assert.InRange(result.Matches[0].Score, .999, 1.001);
            Assert.InRange(result.Matches[1].Score, -.001, .001);
            Assert.Equal("first", result.Matches[0].Record.Payload);
            Assert.Equal([1f, 0f, 0f], result.Matches[0].Record.Values);
            var threshold = await Client.QueryVectorsAsync<string>(Store, collection,
                query with { ScoreThreshold = .5 }, cancellationToken: Ct);
            Assert.Equal("closest", Assert.Single(threshold.Matches).Record.Id);
            var byId = new VectorQuery { ById = "closest", TopK = 1 };
            var shaped =
                Assert.Single((await Client.QueryVectorsAsync<string>(Store, collection, byId, cancellationToken: Ct))
                    .Matches);
            Assert.Null(shaped.Record.Payload);
            Assert.Empty(shaped.Record.Values);
            var batch = await Client.BatchQueryVectorsAsync<string>(Store, collection,
                [byId, new() { ById = "missing" }, query with { Metric = DistanceMetric.Euclidean }, query],
                cancellationToken: Ct);
            Assert.Equal(4, batch.Count);
            // Meilisearch's similar-document query excludes the source record.
            Assert.Equal("other", Assert.Single(batch[0].Response!.Matches).Record.Id);
            Assert.Equal((int)StatusCode.NotFound, batch[1].Error!.Code);
            Assert.Null(batch[1].Response);
            Assert.Equal((int)StatusCode.InvalidArgument, batch[2].Error!.Code);
            Assert.Equal(2, batch[3].Response!.Matches.Count);
        }
        finally
        {
            await Client.DeleteCollectionAsync(Store, collection, cancellationToken: Ct);
        }
    }

    [VectorFact]
    public async Task PartialFailuresDoNotDiscardValidRecords()
    {
        var collection = NewCollection();
        await Client.CreateCollectionAsync(Store, collection, 3, cancellationToken: Ct);
        try
        {
            var write = await Client.UpsertVectorsAsync<byte[]>(Store, collection,
                [new("good", [1, 0, 0]), new("dimensions", [1]), new("nonfinite", [float.NaN, 0, 1])], Wait,
                cancellationToken: Ct);
            Assert.Equal(IndexAck.Completed, write.Ack);
            Assert.Equal(["dimensions", "nonfinite"], write.FailedItems.Select(item => item.Id));
            Assert.All(write.FailedItems, item =>
            {
                Assert.Equal((int)StatusCode.InvalidArgument, item.ErrorCode);
                Assert.NotEmpty(item.ErrorMessage);
            });
            Assert.Equal("good", Assert.Single(await Client.GetVectorsAsync<byte[]>(Store, collection,
                ["good", "dimensions", "nonfinite"], cancellationToken: Ct)).Id);
        }
        finally
        {
            await Client.DeleteCollectionAsync(Store, collection, cancellationToken: Ct);
        }
    }

    [VectorFact]
    public async Task DefaultWritesAcknowledgeAcceptanceAndEventuallyBecomeVisible()
    {
        var collection = NewCollection();
        await Client.CreateCollectionAsync(Store, collection, 3, cancellationToken: Ct);
        try
        {
            var write = await Client.UpsertVectorsAsync<byte[]>(Store, collection, [new("queued", [1, 0, 0])],
                cancellationToken: Ct);
            Assert.Equal(IndexAck.Queued, write.Ack);
            Assert.Empty(write.FailedItems);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            while ((await Client.GetVectorsAsync<byte[]>(Store, collection, ["queued"],
                       cancellationToken: timeout.Token)).Count == 0)
                await Task.Delay(100, timeout.Token);
            Assert.Equal(IndexAck.Queued,
                await Client.DeleteVectorsAsync(Store, collection, ["queued"], cancellationToken: Ct));
            while ((await Client.GetVectorsAsync<byte[]>(Store, collection, ["queued"],
                       cancellationToken: timeout.Token)).Count != 0)
                await Task.Delay(100, timeout.Token);
        }
        finally
        {
            await Client.DeleteCollectionAsync(Store, collection, cancellationToken: Ct);
        }
    }

    [VectorFact]
    public async Task RequestWideErrorsPreserveProviderStatus()
    {
        var error = await Assert.ThrowsAsync<DaprException>(() =>
            Client.GetCollectionAsync(Store, NewCollection(), cancellationToken: Ct));
        Assert.Equal(StatusCode.NotFound, Assert.IsType<RpcException>(error.InnerException).StatusCode);
        error = await Assert.ThrowsAsync<DaprException>(() => Client.BatchQueryVectorsAsync<byte[]>(Store,
            NewCollection(),
            [new() { Vector = [1, 0, 0] }], cancellationToken: Ct));
        Assert.Equal(StatusCode.NotFound, Assert.IsType<RpcException>(error.InnerException).StatusCode);
    }

    private static string NewCollection() => $"sdk-{Guid.NewGuid():N}";
    private static JsonElement Attributes(string category) => JsonSerializer.SerializeToElement(new { category });
}
