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
using System.Text.Json.Serialization;
using Dapr.Common.Serialization;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Proto = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Vector.Test;

public sealed class DaprVectorClientTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly IReadOnlyDictionary<string, string> Metadata =
        new Dictionary<string, string> { ["setting"] = "value" };

    internal static DaprVectorClient CreateClient(VectorCallInvoker invoker, JsonSerializerOptions? options = null) =>
        new DaprVectorGrpcClient(new Proto.Dapr.DaprClient(invoker), new HttpClient(),
            new JsonDaprSerializer(options ?? new JsonSerializerOptions(JsonSerializerDefaults.Web)), "test-token");

    [Fact]
    public async Task CollectionLifecycleMapsAllFieldsAndSnapshotsProperties()
    {
        var invoker = new VectorCallInvoker { Response = new Empty() };
        using var client = CreateClient(invoker);
        await client.CreateCollectionAsync("store", "books", 3, DistanceMetric.DotProduct, Metadata, Ct);
        var create = Assert.IsType<Proto.CreateCollectionRequestAlpha1>(invoker.Request);
        Assert.Equal(("store", "books", 3U, Proto.DistanceMetric.DotProduct, "value"),
            (create.StoreName, create.Collection, create.Dimensions, create.Metric, create.Metadata["setting"]));
        Assert.Equal("/dapr.proto.runtime.v1.Dapr/CreateCollectionAlpha1", invoker.Method);
        Assert.Equal(Ct, invoker.Options.CancellationToken);
        Assert.Contains(invoker.Options.Headers!, h => h.Key == "dapr-api-token" && h.Value == "test-token");
        Assert.Contains(invoker.Options.Headers!, h => h.Key == "user-agent");
        var response = new Proto.GetCollectionResponseAlpha1
        {
            Collection = "books",
            RecordCount = ulong.MaxValue,
            Dimensions = 3,
            Metric = Proto.DistanceMetric.Euclidean,
            Properties = { { "setting", "original" } }
        };
        invoker.Response = response;
        var info = await client.GetCollectionAsync("store", "books", Metadata, Ct);
        var get = Assert.IsType<Proto.GetCollectionRequestAlpha1>(invoker.Request);
        Assert.Equal(("store", "books", "value"), (get.StoreName, get.Collection, get.Metadata["setting"]));
        Assert.Equal(("books", ulong.MaxValue, 3U, DistanceMetric.Euclidean),
            (info.Collection, info.RecordCount, info.Dimensions, info.Metric));
        response.Properties["setting"] = "changed";
        Assert.Equal("original", info.Properties["setting"]);
        invoker.Response = new Proto.ListCollectionsResponseAlpha1 { Collections = { "books", "films" } };
        Assert.Equal(["books", "films"], await client.ListCollectionsAsync("store", Metadata, Ct));
        var list = Assert.IsType<Proto.ListCollectionsRequestAlpha1>(invoker.Request);
        Assert.Equal(("store", "value"), (list.StoreName, list.Metadata["setting"]));
        invoker.Response = new Empty();
        await client.DeleteCollectionAsync("store", "books", Metadata, Ct);
        var delete = Assert.IsType<Proto.DeleteCollectionRequestAlpha1>(invoker.Request);
        Assert.Equal(("store", "books", "value"), (delete.StoreName, delete.Collection, delete.Metadata["setting"]));
        Assert.Equal(4, invoker.DisposeCount);
    }

    [Theory]
    [InlineData(IndexAck.Queued)]
    [InlineData(IndexAck.Completed)]
    public async Task UpsertsTypedPayloadMetadataAndRichFailures(IndexAck ack)
    {
        var invoker = new VectorCallInvoker
        {
            Response = new Proto.UpsertVectorsResponseAlpha1
            {
                Ack = (Proto.IndexAck)ack,
                FailedItems = { new Proto.FailedItem { Id = "bad", Error = Error() } }
            }
        };
        using var client = CreateClient(invoker, VectorJsonContext.Default.Options);
        var attributes =
            JsonSerializer.Deserialize<JsonElement>("""{"category":"books","nested":{"available":true}}""");
        var result = await client.UpsertVectorsAsync("store", "books",
            new[] { new VectorRecord<Book>("1", [1, 2, 3], new Book("Dapr"), attributes) },
            metadata: Metadata, cancellationToken: Ct);
        var request = Assert.IsType<Proto.UpsertVectorsRequestAlpha1>(invoker.Request);
        Assert.Equal(("store", "books", "value"), (request.StoreName, request.Collection, request.Metadata["setting"]));
        Assert.Null(request.Options);
        var record = Assert.Single(request.Records);
        Assert.Equal([1f, 2f, 3f], record.Values);
        Assert.Equal("""{"Title":"Dapr"}""", record.Payload.ToStringUtf8());
        Assert.Equal("books", record.Metadata.Fields["category"].StringValue);
        Assert.Equal(ack, result.Ack);
        var failure = Assert.Single(result.FailedItems);
        Assert.Equal(("bad", 3, "bad vector"), (failure.Id, failure.ErrorCode, failure.ErrorMessage));
        Assert.Equal(new byte[] { 1, 2, 3 }, Assert.Single(failure.Details).Value.ToArray());
        Assert.Equal("type.example/detail", failure.Details[0].TypeUrl);
        invoker.Response = new Proto.GetVectorsResponseAlpha1 { Records = { record } };
        var found = Assert.Single(
            await client.GetVectorsAsync<Book>("store", "books", ["1"], true, cancellationToken: Ct));
        Assert.Equal(new Book("Dapr"), found.Payload);
        record.Values[0] = 99;
        record.Metadata.Fields["category"] = Value.ForString("changed");
        Assert.Equal(1, found.Values[0]);
        Assert.Equal("books", found.Metadata!.Value.GetProperty("category").GetString());
    }

    [Fact]
    public async Task OpaquePayloadsRoundTripExactlyIncludingEmptyAndAbsent()
    {
        var invoker = new VectorCallInvoker
        {
            Response = new Proto.UpsertVectorsResponseAlpha1 { Ack = Proto.IndexAck.Completed }
        };
        using var client = CreateClient(invoker,
            new JsonSerializerOptions { TypeInfoResolver = VectorJsonContext.Default });
        await client.UpsertVectorsAsync<byte[]>("s", "c",
            [new("bytes", [1], [0, 255, 128]), new("empty", [1], []), new("absent", [1])], cancellationToken: Ct);
        var records = Assert.IsType<Proto.UpsertVectorsRequestAlpha1>(invoker.Request).Records;
        Assert.Equal(new byte[] { 0, 255, 128 }, records[0].Payload.ToByteArray());
        Assert.True(records[1].HasPayload);
        Assert.False(records[2].HasPayload);
        invoker.Response = new Proto.GetVectorsResponseAlpha1 { Records = { records } };
        var found = await client.GetVectorsAsync<byte[]>("s", "c", ["bytes", "empty", "absent"], cancellationToken: Ct);
        Assert.Equal(new byte[] { 0, 255, 128 }, found[0].Payload);
        Assert.Empty(found[1].Payload!);
        Assert.Null(found[2].Payload);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetPreservesOptionalFlagOrderAndOmittedPayloads(bool? includeValues)
    {
        var invoker = new VectorCallInvoker
        {
            Response = new Proto.GetVectorsResponseAlpha1
            {
                Records = { new Proto.VectorRecord { Id = "b" }, new Proto.VectorRecord { Id = "a" } }
            }
        };
        using var client = CreateClient(invoker);
        var found = await client.GetVectorsAsync<Book>("s", "c", ["b", "missing", "a"], includeValues, Metadata, Ct);
        var request = Assert.IsType<Proto.GetVectorsRequestAlpha1>(invoker.Request);
        Assert.Equal(("s", "c", "value"), (request.StoreName, request.Collection, request.Metadata["setting"]));
        Assert.Equal(["b", "missing", "a"], request.Ids);
        Assert.Equal(includeValues.HasValue, request.HasIncludeValues);
        Assert.Equal(includeValues ?? false, request.IncludeValues);
        Assert.Equal(["b", "a"], found.Select(record => record.Id));
        Assert.All(found, record =>
        {
            Assert.Empty(record.Values);
            Assert.Null(record.Payload);
            Assert.Null(record.Metadata);
        });
    }

    [Theory]
    [InlineData(IndexAck.Queued, IndexingWaitTimeoutAction.ContinueAsync)]
    [InlineData(IndexAck.Completed, IndexingWaitTimeoutAction.FailRequest)]
    public async Task BothWritesForwardWaitPolicies(IndexAck ack, IndexingWaitTimeoutAction action)
    {
        var invoker = new VectorCallInvoker
        {
            Response = new Proto.DeleteVectorsResponseAlpha1 { Ack = (Proto.IndexAck)ack }
        };
        using var client = CreateClient(invoker);
        var options = new IndexingOptions
        {
            Mode = IndexingMode.WaitForCompletion,
            WaitTimeout = TimeSpan.FromMilliseconds(1250),
            OnWaitTimeout = action
        };
        Assert.Equal(ack, await client.DeleteVectorsAsync("s", "c", ["1"], options, Metadata, Ct));
        var delete = Assert.IsType<Proto.DeleteVectorsRequestAlpha1>(invoker.Request);
        Assert.Equal(("s", "c", "value"), (delete.StoreName, delete.Collection, delete.Metadata["setting"]));
        Assert.Equal(["1"], delete.Ids);
        Assert.Equal(options.WaitTimeout, delete.Options.WaitTimeout.ToTimeSpan());
        Assert.Equal((Proto.IndexingWaitTimeoutAction)action, delete.Options.OnWaitTimeout);
        Assert.Equal(Proto.IndexingMode.WaitForCompletion, delete.Options.Mode);
        invoker.Response = new Proto.UpsertVectorsResponseAlpha1 { Ack = (Proto.IndexAck)ack };
        await client.UpsertVectorsAsync<Book>("s", "c", [], options, cancellationToken: Ct);
        Assert.Equal(delete.Options, Assert.IsType<Proto.UpsertVectorsRequestAlpha1>(invoker.Request).Options);
    }

    [Theory]
    [InlineData(DistanceMetric.Cosine, -0.5)]
    [InlineData(DistanceMetric.DotProduct, 500)]
    [InlineData(DistanceMetric.Euclidean, 0.25)]
    public async Task QueriesMapAllFieldsAndPreserveUnnormalizedScores(DistanceMetric metric, double score)
    {
        var invoker = new VectorCallInvoker { Response = Response(metric, score) };
        using var client = CreateClient(invoker);
        var query = new VectorQuery
        {
            Vector = [1, 0, -1],
            TopK = 0,
            IncludeValues = false,
            IncludePayload = true,
            Metric = metric,
            ScoreThreshold = score,
            Filter = JsonSerializer.Deserialize<JsonElement>("""{"category":{"$eq":"books"}}""")
        };
        var result = await client.QueryVectorsAsync<byte[]>("s", "c", query, Metadata, Ct);
        var request = Assert.IsType<Proto.QueryVectorsRequestAlpha1>(invoker.Request);
        Assert.Equal(("s", "c", "value"), (request.StoreName, request.Collection, request.Metadata["setting"]));
        Assert.Equal(Proto.VectorQuery.KindOneofCase.Vector, request.Query.KindCase);
        Assert.Equal([1f, 0f, -1f], request.Query.Vector.Values);
        Assert.True(request.Query.HasTopK);
        Assert.Equal(0U, request.Query.TopK);
        Assert.True(request.Query.HasIncludeValues);
        Assert.False(request.Query.IncludeValues);
        Assert.True(request.Query.IncludePayload);
        Assert.Equal((Proto.DistanceMetric)metric, request.Query.Metric);
        Assert.Equal(score, request.Query.ScoreThreshold);
        Assert.Equal("books", request.Query.Filter.Fields["category"].StructValue.Fields["$eq"].StringValue);
        Assert.Equal(metric, result.Metric);
        Assert.Equal(score, Assert.Single(result.Matches).Score);
        await client.QueryVectorsAsync<byte[]>("s", "c", new VectorQuery { ById = "stored" }, cancellationToken: Ct);
        var byId = Assert.IsType<Proto.QueryVectorsRequestAlpha1>(invoker.Request).Query;
        Assert.Equal(Proto.VectorQuery.KindOneofCase.ById, byId.KindCase);
        Assert.Equal("stored", byId.ById);
        Assert.False(byId.HasTopK);
        Assert.False(byId.HasIncludeValues);
        Assert.False(byId.HasIncludePayload);
        Assert.False(byId.HasScoreThreshold);
        Assert.Null(byId.Filter);
        Assert.Equal(Proto.DistanceMetric.Unspecified, byId.Metric);
    }

    [Fact]
    public async Task BatchPreservesOrderedSuccessesErrorsAndDetails()
    {
        var invoker = new VectorCallInvoker
        {
            Response = new Proto.BatchQueryVectorsResponseAlpha1
            {
                Results =
                {
                    new Proto.BatchQueryResultAlpha1 { Response = Response(DistanceMetric.Cosine, .8) },
                    new Proto.BatchQueryResultAlpha1 { Error = Error() },
                    new Proto.BatchQueryResultAlpha1 { Response = Response(DistanceMetric.Euclidean, 12) }
                }
            }
        };
        using var client = CreateClient(invoker);
        var results = await client.BatchQueryVectorsAsync<byte[]>("s", "c",
        [
            new() { Vector = [1] }, new() { ById = "missing" },
            new() { ById = "exists", Metric = DistanceMetric.Euclidean }
        ], Metadata, Ct);
        var request = Assert.IsType<Proto.BatchQueryVectorsRequestAlpha1>(invoker.Request);
        Assert.Equal(("s", "c", "value"), (request.StoreName, request.Collection, request.Metadata["setting"]));
        Assert.Equal("missing", request.Queries[1].ById);
        Assert.Equal(3, results.Count);
        Assert.Equal(.8, Assert.Single(results[0].Response!.Matches).Score);
        Assert.Null(results[0].Error);
        Assert.Null(results[1].Response);
        Assert.Equal(3, results[1].Error!.Code);
        Assert.Equal(new byte[] { 1, 2, 3 }, Assert.Single(results[1].Error!.Details).Value.ToArray());
        Assert.Equal(DistanceMetric.Euclidean, results[2].Response!.Metric);
    }

    [Fact]
    public async Task MalformedResponsesDoNotLookSuccessful()
    {
        var invoker = new VectorCallInvoker();
        using var client = CreateClient(invoker);
        foreach (var ack in new[] { 0, 42 })
        {
            invoker.Response = new Proto.UpsertVectorsResponseAlpha1 { Ack = (Proto.IndexAck)ack };
            await Assert.ThrowsAsync<DaprException>(() =>
                client.UpsertVectorsAsync<Book>("s", "c", [], cancellationToken: Ct));
            invoker.Response = new Proto.DeleteVectorsResponseAlpha1 { Ack = (Proto.IndexAck)ack };
            await Assert.ThrowsAsync<DaprException>(() =>
                client.DeleteVectorsAsync("s", "c", [], cancellationToken: Ct));
        }

        foreach (var error in new Google.Rpc.Status?[] { null, new() })
        {
            invoker.Response = new Proto.UpsertVectorsResponseAlpha1
            {
                Ack = Proto.IndexAck.Completed, FailedItems = { new Proto.FailedItem { Error = error } }
            };
            await Assert.ThrowsAsync<DaprException>(() =>
                client.UpsertVectorsAsync<Book>("s", "c", [], cancellationToken: Ct));
        }

        foreach (var batch in new[]
                 {
                     new Proto.BatchQueryVectorsResponseAlpha1(),
                     new Proto.BatchQueryVectorsResponseAlpha1 { Results = { new Proto.BatchQueryResultAlpha1() } },
                     new Proto.BatchQueryVectorsResponseAlpha1
                     {
                         Results = { new Proto.BatchQueryResultAlpha1 { Error = new Google.Rpc.Status() } }
                     }
                 })
        {
            invoker.Response = batch;
            await Assert.ThrowsAsync<DaprException>(() =>
                client.BatchQueryVectorsAsync<Book>("s", "c", [new() { ById = "a" }], cancellationToken: Ct));
        }

        foreach (var metric in new[] { 0, 42 })
        {
            invoker.Response = new Proto.QueryVectorsResponseAlpha1 { Metric = (Proto.DistanceMetric)metric };
            await Assert.ThrowsAsync<DaprException>(() =>
                client.QueryVectorsAsync<Book>("s", "c", new() { ById = "a" }, cancellationToken: Ct));
        }

        invoker.Response = new Proto.QueryVectorsResponseAlpha1
        {
            Metric = Proto.DistanceMetric.Cosine, Matches = { new Proto.VectorMatch() }
        };
        await Assert.ThrowsAsync<DaprException>(() =>
            client.QueryVectorsAsync<Book>("s", "c", new() { ById = "a" }, cancellationToken: Ct));
    }

    [Fact]
    public async Task InvalidArgumentsFailBeforeRpc()
    {
        var invoker = new VectorCallInvoker();
        using var client = CreateClient(invoker);
        foreach (var invalid in new[] { null, "", " " })
        {
            foreach (var operation in Operations(client, invalid!, "c"))
                await Assert.ThrowsAnyAsync<ArgumentException>(operation);
            foreach (var operation in Operations(client, "s", invalid!).Skip(1))
                await Assert.ThrowsAnyAsync<ArgumentException>(operation);
        }

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.CreateCollectionAsync("s", "c", 0, cancellationToken: Ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.CreateCollectionAsync("s", "c", 1, (DistanceMetric)42, cancellationToken: Ct));
        foreach (var query in new VectorQuery?[]
                 {
                     null, new(), new() { Vector = [], ById = "a" }, new() { Vector = [] }, new() { ById = " " },
                     new() { ById = "a", Metric = (DistanceMetric)42 },
                     new() { ById = "a", Filter = JsonSerializer.Deserialize<JsonElement>("[]") },
                     new() { ById = "a", Filter = default(JsonElement) }
                 })
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                client.QueryVectorsAsync<Book>("s", "c", query!, cancellationToken: Ct));
            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                client.BatchQueryVectorsAsync<Book>("s", "c", [query!], cancellationToken: Ct));
        }

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.BatchQueryVectorsAsync<Book>("s", "c", null!, cancellationToken: Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.UpsertVectorsAsync<Book>("s", "c", null!, cancellationToken: Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.UpsertVectorsAsync<Book>("s", "c", [null!], cancellationToken: Ct));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.UpsertVectorsAsync<Book>("s", "c", [new("", [1])], cancellationToken: Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.UpsertVectorsAsync<Book>("s", "c", [new("a", null!)], cancellationToken: Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => client.UpsertVectorsAsync<Book>("s", "c",
            [new("a", [1], Metadata: JsonSerializer.Deserialize<JsonElement>("[]"))], cancellationToken: Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.GetVectorsAsync<Book>("s", "c", null!, cancellationToken: Ct));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.GetVectorsAsync<Book>("s", "c", [""], cancellationToken: Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.DeleteVectorsAsync("s", "c", null!, cancellationToken: Ct));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.DeleteVectorsAsync("s", "c", [" "], cancellationToken: Ct));
        Assert.Null(invoker.Request);
    }

    [Fact]
    public async Task InvalidWaitPoliciesFailBeforeEitherWrite()
    {
        var invoker = new VectorCallInvoker();
        using var client = CreateClient(invoker);
        foreach (var options in new[]
                 {
                     new IndexingOptions { Mode = (IndexingMode)42 },
                     new IndexingOptions { WaitTimeout = TimeSpan.FromSeconds(1) },
                     new IndexingOptions { OnWaitTimeout = IndexingWaitTimeoutAction.FailRequest },
                     new IndexingOptions { Mode = IndexingMode.WaitForCompletion },
                     new IndexingOptions
                     {
                         Mode = IndexingMode.WaitForCompletion,
                         WaitTimeout = TimeSpan.Zero,
                         OnWaitTimeout = IndexingWaitTimeoutAction.FailRequest
                     },
                     new IndexingOptions
                     {
                         Mode = IndexingMode.WaitForCompletion,
                         WaitTimeout = TimeSpan.FromSeconds(-1),
                         OnWaitTimeout = IndexingWaitTimeoutAction.FailRequest
                     },
                     new IndexingOptions
                     {
                         Mode = IndexingMode.WaitForCompletion, WaitTimeout = TimeSpan.FromSeconds(1)
                     },
                     new IndexingOptions
                     {
                         Mode = IndexingMode.WaitForCompletion,
                         WaitTimeout = TimeSpan.FromSeconds(1),
                         OnWaitTimeout = (IndexingWaitTimeoutAction)42
                     }
                 })
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                client.UpsertVectorsAsync<Book>("s", "c", [], options, cancellationToken: Ct));
            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                client.DeleteVectorsAsync("s", "c", [], options, cancellationToken: Ct));
        }

        Assert.Null(invoker.Request);
    }

    [Theory]
    [InlineData(StatusCode.NotFound)]
    [InlineData(StatusCode.DeadlineExceeded)]
    [InlineData(StatusCode.Unimplemented)]
    [InlineData(StatusCode.Cancelled)]
    public async Task RpcErrorsPreserveStatusAndDisposeCalls(StatusCode code)
    {
        var rpc = new RpcException(new Status(code, "failure"));
        var invoker = new VectorCallInvoker { Handler = _ => Task.FromException<object>(rpc) };
        using var client = CreateClient(invoker);
        foreach (var operation in Operations(client, "s", "c"))
            Assert.Same(rpc, (await Assert.ThrowsAsync<DaprException>(operation)).InnerException);
        Assert.Equal(9, invoker.DisposeCount);
    }

    [Fact]
    public async Task CancellationAndDisposalPreventCalls()
    {
        var invoker = new VectorCallInvoker();
        using var client = CreateClient(invoker);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        cancellation.Cancel();
        foreach (var operation in Operations(client, "s", "c", cancellation.Token))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(operation);
        client.Dispose();
        client.Dispose();
        foreach (var operation in Operations(client, "s", "c"))
            await Assert.ThrowsAsync<ObjectDisposedException>(operation);
        Assert.Null(invoker.Request);
    }

    [Fact]
    public async Task CancellationDuringRpcPreservesCallerToken()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var invoker = new VectorCallInvoker
        {
            Handler = _ =>
            {
                cancellation.Cancel();
                return Task.FromException<object>(new RpcException(new Status(StatusCode.Cancelled, "cancelled")));
            }
        };
        using var client = CreateClient(invoker);
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.QueryVectorsAsync<Book>("s", "c", new() { ById = "a" }, cancellationToken: cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(1, invoker.DisposeCount);
    }

    private static IEnumerable<Func<Task>> Operations(DaprVectorClient client, string store, string collection,
        CancellationToken? token = null)
    {
        var ct = token ?? Ct;
        yield return () => client.ListCollectionsAsync(store, cancellationToken: ct);
        yield return () => client.CreateCollectionAsync(store, collection, 3, cancellationToken: ct);
        yield return () => client.GetCollectionAsync(store, collection, cancellationToken: ct);
        yield return () => client.DeleteCollectionAsync(store, collection, cancellationToken: ct);
        yield return () => client.UpsertVectorsAsync<Book>(store, collection, [], cancellationToken: ct);
        yield return () => client.GetVectorsAsync<Book>(store, collection, [], cancellationToken: ct);
        yield return () => client.DeleteVectorsAsync(store, collection, [], cancellationToken: ct);
        yield return () =>
            client.QueryVectorsAsync<Book>(store, collection, new() { ById = "a" }, cancellationToken: ct);
        yield return () =>
            client.BatchQueryVectorsAsync<Book>(store, collection, [new() { ById = "a" }], cancellationToken: ct);
    }

    internal static Proto.QueryVectorsResponseAlpha1 Response(DistanceMetric metric, double score) => new()
    {
        Metric = (Proto.DistanceMetric)metric,
        Matches =
        {
            new Proto.VectorMatch
            {
                Record = new Proto.VectorRecord { Id = "1", Payload = ByteString.CopyFrom([0, 255]) },
                Score = score
            }
        }
    };

    private static Google.Rpc.Status Error() => new()
    {
        Code = 3,
        Message = "bad vector",
        Details = { new Any { TypeUrl = "type.example/detail", Value = ByteString.CopyFrom([1, 2, 3]) } }
    };
}

public sealed record Book(string Title);

[JsonSerializable(typeof(Book))]
internal partial class VectorJsonContext : JsonSerializerContext;
