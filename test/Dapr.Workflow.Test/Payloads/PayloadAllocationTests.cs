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

using System.Runtime.CompilerServices;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapr.BinaryStore;
using Dapr.Common.Serialization;
using Dapr.Workflow.Payloads;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace Dapr.Workflow.Test.Payloads;

public class PayloadAllocationTests
{
    private static readonly PayloadSite Site = new(PayloadKind.WorkflowInput, "instance", "workflow");

    [Fact]
    public void FastPathsDoNotExpandPublicContracts()
    {
        Assert.False(typeof(WorkflowPayloadByteCache).IsVisible);
        Assert.False(typeof(WorkflowPayloadJsonSerializer).IsVisible);
        Assert.Same(typeof(DaprWorkflowClient).Assembly, typeof(WorkflowPayloadJsonSerializer).Assembly);
        Assert.DoesNotContain(typeof(JsonDaprSerializer).GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static),
            method => method.Name.Contains("Workflow", StringComparison.Ordinal));
        Assert.Equal(4, typeof(IDaprSerializer).GetMethods().Length);
    }

    [Fact]
    public async Task CancellationTakesPrecedenceOverMalformedEnvelopeParsing()
    {
        using var store = new Store([]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var handler = new WorkflowPayloadHandler(new JsonDaprSerializer(), CreateCodec(store), cancellation.Token);
        const string malformed = "{\"$daprRef\":";
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await handler.DecodeAsync<string>(malformed, Site, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await handler.DecodeAsync(malformed, typeof(string), Site, cancellation.Token));
        Assert.Equal(0, store.Reads);
    }

    [Theory]
    [InlineData("generic")]
    [InlineData("runtime")]
    [InlineData("resolver")]
    [InlineData("string")]
    public async Task RehydrationDoesNotAllocateRedundantPayloadCopies(string path)
    {
        var serializer = new JsonDaprSerializer();
        var expected = new Document { Content = new string('x', 2_000_000) };
        using var store = new Store(Encoding.UTF8.GetBytes(serializer.Serialize(expected)));
        var codec = CreateCodec(store);
        var handler = new WorkflowPayloadHandler(serializer, codec);
        var resolver = new BinaryStoreWorkflowPayloadResolver(codec, serializer);
        var wire = store.Reference.ToEnvelope();
        var token = TestContext.Current.CancellationToken;

        async Task<object?> ReadAsync() => path switch
        {
            "generic" => await handler.DecodeAsync<Document>(wire, Site, token),
            "runtime" => await handler.DecodeAsync(wire, typeof(Document), Site, token),
            "resolver" => await resolver.ResolveAsync<Document>(store.Reference, token),
            _ => await codec.ResolveStringAsync(store.Reference, token)
        };

        await ReadAsync();
        // The in-memory iterator and all awaits complete synchronously, isolating this operation.
        var thread = Environment.CurrentManagedThreadId;
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = await ReadAsync();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(thread, Environment.CurrentManagedThreadId);
        Assert.InRange(allocated, 1, 6_500_000);
        if (result is Document document)
            Assert.Equal(expected.Content, document.Content);
        else
            Assert.Equal(serializer.Serialize(expected), result);
        Assert.Equal(2, store.Reads);
        Assert.Equal(2, store.DisposedReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FastReadsRespectJsonOptionsAndConverters(bool runtimeType)
    {
        var converter = new DocumentConverter();
        var serializer = new JsonDaprSerializer(new JsonSerializerOptions { Converters = { converter } });
        using var store = new Store(Encoding.UTF8.GetBytes("\"custom\""));
        var handler = new WorkflowPayloadHandler(serializer, CreateCodec(store));
        var result = runtimeType
            ? (Document?)await handler.DecodeAsync(store.Reference.ToEnvelope(), typeof(Document), Site,
                TestContext.Current.CancellationToken)
            : await handler.DecodeAsync<Document>(store.Reference.ToEnvelope(), Site,
                TestContext.Current.CancellationToken);
        Assert.Equal("custom-converted", result!.Content);
        Assert.Equal(1, converter.Reads);
        var resolver = new BinaryStoreWorkflowPayloadResolver(CreateCodec(store), serializer);
        await resolver.StoreAsync(new Document { Content = "outbound" }, "instance",
            TestContext.Current.CancellationToken);
        Assert.Equal("\"outbound\"", Encoding.UTF8.GetString(store.LastWrite!));
    }

    [Fact]
    public async Task CustomSerializersAndDerivedSerializersAreNotBypassed()
    {
        foreach (IDaprSerializer serializer in
                 new IDaprSerializer[] { new CustomSerializer(), new DerivedSerializer() })
        {
            using var store = new Store(Encoding.UTF8.GetBytes("not-json"));
            var codec = CreateCodec(store);
            var handler = new WorkflowPayloadHandler(serializer, codec);
            Assert.Equal("not-json",
                await handler.DecodeAsync<string>(store.Reference.ToEnvelope(), Site,
                    TestContext.Current.CancellationToken));
            Assert.Equal("not-json",
                await handler.DecodeAsync(store.Reference.ToEnvelope(), typeof(string), Site,
                    TestContext.Current.CancellationToken));
            var resolver = new BinaryStoreWorkflowPayloadResolver(codec, serializer);
            Assert.Equal("not-json",
                await resolver.ResolveAsync<string>(store.Reference, TestContext.Current.CancellationToken));
            await resolver.StoreAsync("unchanged", "instance", TestContext.Current.CancellationToken);
            Assert.Equal("custom:unchanged", Encoding.UTF8.GetString(store.LastWrite!));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SplitMultibyteAndMalformedUtf8PreserveStringPathBehavior(bool malformed)
    {
        var bytes = malformed
            ? new byte[] { (byte)'"', 0xC3, (byte)'"' }
            : Encoding.UTF8.GetBytes("\"\u00e9\U0001f642\"");
        using var store = new Store(bytes) { ChunkSize = 1 };
        var serializer = new JsonDaprSerializer();
        var handler = new WorkflowPayloadHandler(serializer, CreateCodec(store));
        var expected = serializer.Deserialize<string>(Encoding.UTF8.GetString(bytes));
        Assert.Equal(expected,
            await handler.DecodeAsync<string>(store.Reference.ToEnvelope(), Site,
                TestContext.Current.CancellationToken));
        Assert.Equal(expected,
            await handler.DecodeAsync(store.Reference.ToEnvelope(), typeof(string), Site,
                TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1000)]
    public async Task ReadsUseActualLengthRatherThanCapacityOrDeclaredSize(int declaredSize)
    {
        using var store = new Store(Encoding.UTF8.GetBytes("\"value\""));
        var reference = store.Reference with { Size = declaredSize };
        var handler = new WorkflowPayloadHandler(new JsonDaprSerializer(), CreateCodec(store));
        Assert.Equal("value",
            await handler.DecodeAsync<string>(reference.ToEnvelope(), Site, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    public async Task FastReadsEnforceDeclaredAndActualLimitsBeforeDeserialization(int declaredSize)
    {
        using var store = new Store(Encoding.UTF8.GetBytes("\"too large\"")) { ChunkSize = 2 };
        var converter = new DocumentConverter();
        var serializer = new JsonDaprSerializer(new JsonSerializerOptions { Converters = { converter } });
        var options = new BinaryStorePayloadOptions { StoreName = "store", MaxInMemoryRehydrationSize = 5 };
        var handler = new WorkflowPayloadHandler(serializer, CreateCodec(store, options: options));
        var reference = store.Reference with { Size = declaredSize };
        await Assert.ThrowsAsync<PayloadRehydrationLimitException>(async () =>
            await handler.DecodeAsync<Document>(reference.ToEnvelope(), Site, TestContext.Current.CancellationToken));
        Assert.Equal(0, converter.Reads);
        Assert.Equal(declaredSize == 1 ? 1 : 0, store.Reads);
        Assert.Equal(store.Reads, store.DisposedReads);
    }

    [Fact]
    public async Task IntegrityIsCheckedBeforeCallingApplicationConverters()
    {
        using var store = new Store(Encoding.UTF8.GetBytes("\"value\""));
        var converter = new DocumentConverter();
        var handler = new WorkflowPayloadHandler(
            new JsonDaprSerializer(new JsonSerializerOptions { Converters = { converter } }), CreateCodec(store));
        await Assert.ThrowsAsync<PayloadIntegrityException>(async () =>
            await handler.DecodeAsync<Document>((store.Reference with { ContentHash = "wrong" }).ToEnvelope(), Site,
                TestContext.Current.CancellationToken));
        Assert.Equal(0, converter.Reads);
        Assert.Equal(1, store.DisposedReads);
    }

    [Fact]
    public async Task DefaultReadsContinueToObserveDeletionAndCorruption()
    {
        using var store = new Store(Encoding.UTF8.GetBytes("\"value\""));
        var reference = store.Reference;
        var codec = CreateCodec(store);
        Assert.Equal("\"value\"", await codec.ResolveStringAsync(reference, TestContext.Current.CancellationToken));
        store.Bytes = Encoding.UTF8.GetBytes("\"other\"");
        await Assert.ThrowsAsync<PayloadIntegrityException>(() =>
            codec.ResolveStringAsync(reference, TestContext.Current.CancellationToken));
        store.Missing = true;
        await Assert.ThrowsAsync<PayloadNotFoundException>(() =>
            codec.ResolveStringAsync(reference, TestContext.Current.CancellationToken));
        Assert.Equal(3, store.Reads);
        Assert.Equal(3, store.DisposedReads);
    }

    [Fact]
    public async Task CachedBytesNeverReuseMutableApplicationObjects()
    {
        using var store = new Store(Encoding.UTF8.GetBytes("{\"content\":\"original\"}"));
        var codec = CreateCodec(store, new WorkflowPayloadByteCache());
        var handler = new WorkflowPayloadHandler(new JsonDaprSerializer(), codec);
        var wire = store.Reference.ToEnvelope();
        var first = await handler.DecodeAsync<Document>(wire, Site, TestContext.Current.CancellationToken);
        first!.Content = "mutated";
        var second =
            (Document?)await handler.DecodeAsync(wire, typeof(Document), Site, TestContext.Current.CancellationToken);
        var resolver = new BinaryStoreWorkflowPayloadResolver(codec, new JsonDaprSerializer());
        var third = await resolver.ResolveAsync<Document>(store.Reference.AsTyped<Document>(),
            TestContext.Current.CancellationToken);
        Assert.Equal("original", second!.Content);
        Assert.Equal("original", third.Content);
        Assert.NotSame(first, second);
        Assert.NotSame(second, third);
        Assert.Equal(1, store.Reads);
    }

    [Fact]
    public async Task CachedReadsStillEnforceCancellationLimitsStagesAndMetadata()
    {
        using var store = new Store(Encoding.UTF8.GetBytes("\"value\""));
        var options = new BinaryStorePayloadOptions { StoreName = "store" };
        var codec = CreateCodec(store, new WorkflowPayloadByteCache(), options);
        var reference = store.Reference;
        await codec.ResolveBytesAsync(reference, TestContext.Current.CancellationToken);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            codec.ResolveBytesAsync(reference, cancelled.Token));
        await Assert.ThrowsAsync<PayloadIntegrityException>(() =>
            codec.ResolveBytesAsync(reference with { ContentHash = "changed" }, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            codec.ResolveBytesAsync(reference with { Stages = [new PayloadStage("unsupported")] },
                TestContext.Current.CancellationToken));
        options.MaxInMemoryRehydrationSize = 1;
        await Assert.ThrowsAsync<PayloadRehydrationLimitException>(() =>
            codec.ResolveBytesAsync(reference, TestContext.Current.CancellationToken));
        Assert.Equal(2, store.Reads);
    }

    [Fact]
    public async Task FailedAndUnverifiedReadsAreNeverCached()
    {
        using var store = new Store(Encoding.UTF8.GetBytes("\"value\""));
        var options = new BinaryStorePayloadOptions { StoreName = "store" };
        var codec = CreateCodec(store, new WorkflowPayloadByteCache(), options);
        var reference = store.Reference;
        store.Bytes = Encoding.UTF8.GetBytes("\"wrong\"");
        await Assert.ThrowsAsync<PayloadIntegrityException>(() =>
            codec.ResolveBytesAsync(reference, TestContext.Current.CancellationToken));
        store.Bytes = Encoding.UTF8.GetBytes("\"value\"");
        options.VerifyIntegrityOnRead = false;
        await codec.ResolveBytesAsync(reference, TestContext.Current.CancellationToken);
        await codec.ResolveBytesAsync(reference, TestContext.Current.CancellationToken);
        options.VerifyIntegrityOnRead = true;
        await codec.ResolveBytesAsync(reference, TestContext.Current.CancellationToken);
        await codec.ResolveBytesAsync(reference, TestContext.Current.CancellationToken);
        Assert.Equal(4, store.Reads);
    }

    [Fact]
    public async Task CancellationDisposesTheDownloadAndDoesNotPopulateCache()
    {
        using var store = new Store(Encoding.UTF8.GetBytes("\"value\"")) { ChunkSize = 1 };
        var codec = CreateCodec(store, new WorkflowPayloadByteCache());
        using var cancellation = new CancellationTokenSource();
        store.AfterChunk = cancellation.Cancel;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            codec.ResolveBytesAsync(store.Reference, cancellation.Token));
        store.AfterChunk = null;
        await codec.ResolveBytesAsync(store.Reference, TestContext.Current.CancellationToken);
        Assert.Equal(2, store.Reads);
        Assert.Equal(2, store.DisposedReads);
    }

    [Fact]
    public async Task CleanupInvalidatesCachedReadsAndSubsequentWritesReachStorage()
    {
        using var store = new Store(Encoding.UTF8.GetBytes("\"value\""));
        var manifest = new Manifest();
        var codec = CreateCodec(store, new WorkflowPayloadByteCache(), manifest: manifest);
        var reference =
            await codec.StoreSerializedAsync("\"value\"", "instance", TestContext.Current.CancellationToken);
        await codec.ResolveBytesAsync(reference, TestContext.Current.CancellationToken);
        await codec.StoreSerializedAsync("\"value\"", "instance", TestContext.Current.CancellationToken);
        Assert.Equal(2, store.Writes);
        var cleaner = new WorkflowPayloadCleaner(store, manifest, codec);
        Assert.Equal(1, await cleaner.PurgeAsync("instance", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<PayloadNotFoundException>(() =>
            codec.ResolveBytesAsync(reference, TestContext.Current.CancellationToken));
        await codec.StoreSerializedAsync("\"value\"", "instance", TestContext.Current.CancellationToken);
        Assert.Equal(3, store.Writes);
    }

    [Fact]
    public async Task WritesAlwaysReachStorageWithTheCurrentSerializedContent()
    {
        using var store = new Store(Encoding.UTF8.GetBytes("\"value\""));
        var options = new BinaryStorePayloadOptions { StoreName = "store" };
        var codec = CreateCodec(store, new WorkflowPayloadByteCache(), options);
        var reference =
            await codec.StoreSerializedAsync("\"value\"", "instance", TestContext.Current.CancellationToken);
        await codec.ResolveBytesAsync(reference, TestContext.Current.CancellationToken);
        await codec.StoreSerializedAsync("\"value\"", "instance", TestContext.Current.CancellationToken);
        options.SkipWriteIfExists = false;
        await codec.StoreSerializedAsync("\"value\"", "instance", TestContext.Current.CancellationToken);
        options.SkipWriteIfExists = true;
        var changed =
            await codec.StoreSerializedAsync("\"changed\"", "instance", TestContext.Current.CancellationToken);
        Assert.NotEqual(reference.Key, changed.Key);
        Assert.Equal(4, store.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CachedReadsNeverOverrideTheStoresAlreadyExistsResponse(bool skipWriteIfExists)
    {
        var token = TestContext.Current.CancellationToken;
        using var store = new Store(Encoding.UTF8.GetBytes("\"value\""));
        var codec = CreateCodec(store, new WorkflowPayloadByteCache(),
            new BinaryStorePayloadOptions { StoreName = "store", SkipWriteIfExists = skipWriteIfExists });
        var reference = await codec.StoreSerializedAsync("\"value\"", "instance", token);
        await codec.ResolveBytesAsync(reference, token);
        store.WriteError = new DaprException("Already exists", new RpcException(new Status(StatusCode.AlreadyExists, "exists")));
        if (skipWriteIfExists)
            Assert.Equal(reference, await codec.StoreSerializedAsync("\"value\"", "instance", token));
        else
            Assert.Same(store.WriteError, await Assert.ThrowsAsync<DaprException>(() =>
                codec.StoreSerializedAsync("\"value\"", "instance", token)));
        Assert.Equal(2, store.Writes);
    }

    [Fact]
    public async Task ReplicaHandoffsUseIndependentCachesAndReferenceIdentity()
    {
        var token = TestContext.Current.CancellationToken;
        using var sharedStore = new Store(Encoding.UTF8.GetBytes("{\"content\":\"first\"}"));
        var a = new WorkflowPayloadHandler(new JsonDaprSerializer(), CreateCodec(sharedStore, new WorkflowPayloadByteCache()));
        var b = new WorkflowPayloadHandler(new JsonDaprSerializer(), CreateCodec(sharedStore, new WorkflowPayloadByteCache()));
        var wire = sharedStore.Reference.ToEnvelope();
        var first = await a.DecodeAsync<Document>(wire, Site, token);
        first!.Content = "locally mutated";
        Assert.Equal("first", (await b.DecodeAsync<Document>(wire, Site, token))!.Content);
        Assert.Equal("first", (await a.DecodeAsync<Document>(wire, Site, token))!.Content);
        Assert.Equal(2, sharedStore.Reads);

        // Even if a key is reused, new recorded hash metadata cannot hit an old replica's cache.
        sharedStore.Bytes = Encoding.UTF8.GetBytes("{\"content\":\"other\"}");
        var changedWire = sharedStore.Reference.ToEnvelope();
        Assert.Equal("other", (await b.DecodeAsync<Document>(changedWire, Site, token))!.Content);
        Assert.Equal("other", (await a.DecodeAsync<Document>(changedWire, Site, token))!.Content);
        Assert.Equal(4, sharedStore.Reads);
    }

    [Fact]
    public async Task StaleReplicaReadCacheCannotSuppressWritesAfterAnotherReplicaDeletesTheObject()
    {
        var token = TestContext.Current.CancellationToken;
        using var sharedStore = new Store([]);
        var manifest = new Manifest();
        var a = CreateCodec(sharedStore, new WorkflowPayloadByteCache(), manifest: manifest);
        var b = CreateCodec(sharedStore, new WorkflowPayloadByteCache(), manifest: manifest);
        var reference = await a.StoreSerializedAsync("\"value\"", "instance", token);
        await a.ResolveBytesAsync(reference, token);
        Assert.Equal(1, await new WorkflowPayloadCleaner(sharedStore, manifest, b).PurgeAsync("instance", token));
        await Assert.ThrowsAsync<PayloadNotFoundException>(() => b.ResolveBytesAsync(reference, token));

        // A still has its read cache, but shared storage, not that cache, decides whether a write is needed.
        Assert.Equal(reference, await a.StoreSerializedAsync("\"value\"", "instance", token));
        Assert.Equal(2, sharedStore.Writes);
        Assert.Equal("\"value\"", await b.ResolveStringAsync(reference, token));
        Assert.Single(await manifest.ListAsync("instance", token));
    }

    [Fact]
    public void CacheBoundsRetainedCapacityEvictsLruAndExpiresWithoutSliding()
    {
        var time = new Clock();
        var cache = new WorkflowPayloadByteCache(10, time);
        var a = new PayloadReference("store", "a", 1, "hash");
        var b = a with { Key = "b" };
        var c = a with { Key = "c" };
        cache.Add(a, new byte[1], 5, cache.Generation);
        cache.Add(b, new byte[1], 5, cache.Generation);
        Assert.True(cache.TryGet(a, out _));
        cache.Add(c, new byte[1], 5, cache.Generation);
        Assert.False(cache.TryGet(b, out _));
        Assert.True(cache.TryGet(a, out _));
        cache.Add(b, new byte[1], 11, cache.Generation);
        Assert.False(cache.TryGet(b, out _));
        time.Advance(TimeSpan.FromSeconds(59));
        Assert.True(cache.TryGet(a, out _));
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.False(cache.TryGet(a, out _));
        Assert.False(cache.TryGet(c, out _));
    }

    [Fact]
    public void CleanupPreventsInFlightReadsFromRepopulatingCache()
    {
        var cache = new WorkflowPayloadByteCache();
        var reference = new PayloadReference("store", "key", 1, "hash");
        var generation = cache.Generation;
        cache.Invalidate(reference);
        cache.Add(reference, new byte[1], 1, generation);
        Assert.False(cache.TryGet(reference, out _));
        cache.Add(reference, new byte[1], 1, cache.Generation);
        Assert.True(cache.TryGet(reference, out _));
        Assert.False(cache.TryGet(reference with { StoreName = "other" }, out _));
        Assert.False(cache.TryGet(reference with { Size = 2 }, out _));
        Assert.False(cache.TryGet(reference with { Version = 2 }, out _));
    }

    [Fact]
    public async Task DownloadCompletingAfterCleanupDoesNotRestoreTheDeletedEntry()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var store = new Store(Encoding.UTF8.GetBytes("\"value\"")) { EndRead = gate.Task };
        var manifest = new Manifest();
        var reference = store.Reference;
        var token = TestContext.Current.CancellationToken;
        await manifest.RecordAsync("instance", reference, token);
        var codec = CreateCodec(store, new WorkflowPayloadByteCache(), manifest: manifest);
        var pending = codec.ResolveBytesAsync(reference, token);
        Assert.False(pending.IsCompleted);
        try
        {
            Assert.Equal(1, await new WorkflowPayloadCleaner(store, manifest, codec).PurgeAsync("instance", token));
        }
        finally
        {
            gate.TrySetResult();
        }

        Assert.Equal("\"value\"", Encoding.UTF8.GetString((await pending).Span));
        await Assert.ThrowsAsync<PayloadNotFoundException>(() => codec.ResolveBytesAsync(reference, token));
        Assert.Equal(2, store.Reads);
    }

    [Fact]
    public async Task FailedCleanupInvalidatesCacheButPreservesManifestForRetry()
    {
        using var store = new Store(Encoding.UTF8.GetBytes("\"value\""));
        var manifest = new Manifest();
        var reference = store.Reference;
        var token = TestContext.Current.CancellationToken;
        await manifest.RecordAsync("instance", reference, token);
        var codec = CreateCodec(store, new WorkflowPayloadByteCache(), manifest: manifest);
        await codec.ResolveBytesAsync(reference, token);
        store.DeleteError =
            new DaprException("Unavailable", new RpcException(new Status(StatusCode.Unavailable, "retry")));
        var cleaner = new WorkflowPayloadCleaner(store, manifest, codec);
        Assert.Same(store.DeleteError,
            await Assert.ThrowsAsync<DaprException>(() => cleaner.PurgeAsync("instance", token)));
        Assert.Single(await manifest.ListAsync("instance", token));
        await codec.ResolveBytesAsync(reference, token);
        Assert.Equal(2, store.Reads);
        store.DeleteError = null;
        Assert.Equal(1, await cleaner.PurgeAsync("instance", token));
        Assert.Empty(await manifest.ListAsync("instance", token));
    }

    [Fact]
    public async Task ConcurrentCacheAccessAndInvalidationRemainIsolated()
    {
        var cache = new WorkflowPayloadByteCache(1024);
        await Parallel.ForEachAsync(Enumerable.Range(0, 1000), TestContext.Current.CancellationToken,
            (i, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var reference = new PayloadReference("store", i.ToString(), 1, i.ToString());
                cache.Add(reference, new byte[] { (byte)i }, 1, cache.Generation);
                if (cache.TryGet(reference, out var bytes))
                    Assert.Equal((byte)i, bytes.Span[0]);
                cache.Invalidate(reference);
                Assert.False(cache.TryGet(reference, out _));
                return ValueTask.CompletedTask;
            });
    }

    [Fact]
    public void CacheAlsoBoundsEntriesWithEmptyPayloads()
    {
        var cache = new WorkflowPayloadByteCache(1);
        for (var i = 0; i < 300; i++)
            cache.Add(new PayloadReference("store", i.ToString(), 0, "hash"), ReadOnlyMemory<byte>.Empty, 0,
                cache.Generation);
        var retained = Enumerable.Range(0, 300).Count(i =>
            cache.TryGet(new PayloadReference("store", i.ToString(), 0, "hash"), out _));
        Assert.Equal(256, retained);
    }

    [Fact]
    public async Task PooledUploadsContainExactlyTheSerializedBytesAndKeepSnapshotSemantics()
    {
        using var store = new Store([]);
        var codec = CreateCodec(store,
            options: new BinaryStorePayloadOptions
            {
                StoreName = "store", WritePreference = PayloadWritePreference.PreferReference, OffloadThreshold = 1
            });
        var serializer = new JsonDaprSerializer();
        var handler = new WorkflowPayloadHandler(serializer, codec);
        var document = new Document { Content = new string('x', 100_003) + "\u00e9" };
        var snapshot = handler.Snapshot(document);
        document.Content = "modified";
        var wire = await handler.EncodeAsync(snapshot, Site, TestContext.Current.CancellationToken);
        var reference = PayloadReferenceParser.Parse(wire)!;
        var expected = Encoding.UTF8.GetBytes(snapshot);
        Assert.Equal(expected, store.LastWrite);
        Assert.Equal(expected.Length, reference.Size);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(expected)).ToLowerInvariant(), reference.ContentHash);
        await codec.StoreSerializedAsync("short", "instance", TestContext.Current.CancellationToken);
        Assert.Equal("short", Encoding.UTF8.GetString(store.LastWrite!));
    }

    [Fact]
    public void OrdinaryLargeObjectsAreRejectedWithoutParsingOrAllocatingTheirBody()
    {
        var wire = "{\"content\":\"" + new string('x', 2_000_000) + "\"}";
        Assert.Null(PayloadReferenceParser.Parse(wire));
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10; i++)
            Assert.Null(PayloadReferenceParser.Parse(wire));
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0, 4096);
    }

    [Theory]
    [InlineData("{\"content\":{\"$daprRef\":")]
    [InlineData("{\"content\":\"$daprRef\",")]
    public void FastNegativeDetectionPreservesMalformedEnvelopeErrors(string wire)
    {
        Assert.ThrowsAny<JsonException>(() => PayloadReferenceParser.Parse(wire));
    }

    [Fact]
    public void EscapedNamesWhitespaceAndOrdinarySentinelPropertiesRetainTheirMeaning()
    {
        var reference = new PayloadReference("store", "key", 1, "hash");
        var wire = reference.ToEnvelope().Replace("$daprRef", "\\u0024daprRef", StringComparison.Ordinal);
        Assert.Equal(reference, PayloadReferenceParser.Parse(wire));
        Assert.Equal(reference, PayloadReferenceParser.Parse("{" + new string(' ', 256) + wire[1..]));
        Assert.Null(PayloadReferenceParser.Parse("{\"content\":1,\"$daprRef\":{}}"));
        Assert.Null(PayloadReferenceParser.Parse("{\"$daprRef\":{},\"content\":1}"));
        Assert.Null(PayloadReferenceParser.Parse("{\"content\": not-json"));
    }

    private static BinaryStorePayloadConverter CreateCodec(Store store, WorkflowPayloadByteCache? cache = null,
        BinaryStorePayloadOptions? options = null, IWorkflowPayloadManifest? manifest = null) =>
        new(store, Options.Create(options ?? new BinaryStorePayloadOptions { StoreName = "store" }),
            manifest ?? NullWorkflowPayloadManifest.Instance, cache);

    public sealed class Document
    {
        public string Content { get; set; } = "";
    }

    private sealed class DocumentConverter : JsonConverter<Document>
    {
        internal int Reads { get; private set; }

        public override Document Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Reads++;
            return new Document { Content = reader.GetString() + "-converted" };
        }

        public override void Write(Utf8JsonWriter writer, Document value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Content);
    }

    private class CustomSerializer : IDaprSerializer
    {
        public string Serialize<T>(T value) => "custom:" + value;
        public string Serialize(object? value, Type? inputType = null) => "custom:" + value;
        public T? Deserialize<T>(string? data) => (T?)(object?)data;
        public object? Deserialize(string? data, Type returnType) => data;
    }

    private sealed class DerivedSerializer : JsonDaprSerializer, IDaprSerializer
    {
        string IDaprSerializer.Serialize<T>(T value) => "custom:" + value;
        string IDaprSerializer.Serialize(object? value, Type? inputType) => "custom:" + value;
        T? IDaprSerializer.Deserialize<T>(string? data) where T : default => (T?)(object?)data;
        object? IDaprSerializer.Deserialize(string? data, Type returnType) => data;
    }

    private sealed class Clock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        internal void Advance(TimeSpan elapsed) => _ticks += elapsed.Ticks;
    }

    private sealed class Manifest : IWorkflowPayloadManifest
    {
        private PayloadReference? _reference;

        public Task RecordAsync(string instanceId, PayloadReference reference,
            CancellationToken cancellationToken = default)
        {
            _reference = reference;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<PayloadReference>> ListAsync(string instanceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<PayloadReference>>(_reference is null ? [] : [_reference]);

        public Task ClearAsync(string instanceId, CancellationToken cancellationToken = default)
        {
            _reference = null;
            return Task.CompletedTask;
        }
    }

    private sealed class Store(byte[] bytes) : DaprBinaryStoreClient
    {
        internal byte[] Bytes { get; set; } = bytes;
        internal byte[]? LastWrite { get; private set; }
        internal int Reads { get; private set; }
        internal int Writes { get; private set; }
        internal int DisposedReads { get; private set; }
        internal int ChunkSize { get; init; } = 32768;
        internal bool Missing { get; set; }
        internal Action? AfterChunk { get; set; }
        internal Task? EndRead { get; init; }
        internal Exception? DeleteError { get; set; }
        internal Exception? WriteError { get; set; }

        internal PayloadReference Reference => new("store", "key", Bytes.Length,
            Convert.ToHexString(SHA256.HashData(Bytes)).ToLowerInvariant());

        public override async IAsyncEnumerable<ReadOnlyMemory<byte>> GetBinaryFileChunksAsync(
            string storeName, string fileName, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Reads++;
            try
            {
                await Task.CompletedTask;
                if (Missing)
                    throw new DaprException("Missing", new RpcException(new Status(StatusCode.NotFound, "Missing")));
                for (var offset = 0; offset < Bytes.Length; offset += ChunkSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return Bytes.AsMemory(offset, Math.Min(ChunkSize, Bytes.Length - offset));
                    AfterChunk?.Invoke();
                }

                if (EndRead is { } endRead)
                    await endRead.WaitAsync(cancellationToken);
            }
            finally
            {
                DisposedReads++;
            }
        }

        public override async Task SetBinaryFileAsync(string storeName, string fileName, Stream content,
            bool overwrite = false, int chunkSize = DefaultChunkSize, CancellationToken cancellationToken = default)
        {
            Writes++;
            if (WriteError is not null)
                throw WriteError;
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            LastWrite = copy.ToArray();
            Bytes = LastWrite;
            Missing = false;
        }

        public override Task DeleteBinaryFileAsync(string storeName, string fileName,
            CancellationToken cancellationToken = default)
        {
            if (DeleteError is not null)
                return Task.FromException(DeleteError);
            Missing = true;
            return Task.CompletedTask;
        }
    }
}
