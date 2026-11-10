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

using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Text;
using Dapr.BinaryStore;
using Dapr.Common.Serialization;
using Dapr.Workflow.Payloads;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Moq;

namespace Dapr.Workflow.Test.Payloads;

public class PayloadDirectWriteTests
{
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("stream", false)]
    [InlineData("stream", true)]
    [InlineData("chunks", false)]
    [InlineData("chunks", true)]
    [InlineData("producer", false)]
    [InlineData("producer", true)]
    public async Task UploadOverloadsShareRawByteSemantics(string mode, bool cached)
    {
        byte[] expected = [1, 2, 3];
        using var source = new MemoryStream(new byte[] { 0, 1, 2, 3 });
        source.Position = 1;
        var enumerations = 0;
        var disposed = false;
        async IAsyncEnumerable<ReadOnlyMemory<byte>> Chunks()
        {
            enumerations++;
            var reused = new byte[1];
            try
            {
                yield return ReadOnlyMemory<byte>.Empty;
                foreach (var value in expected)
                {
                    await Task.Yield();
                    reused[0] = value;
                    yield return reused;
                }
            }
            finally { disposed = true; }
        }
        var store = UploadStore(async (stream, ct) =>
        {
            using var received = new MemoryStream();
            await stream.CopyToAsync(received, ct);
            Assert.Equal(expected, received.ToArray());
        });
        var resolver = CreateResolver(store);
        if (cached)
            resolver = new CachingWorkflowPayloadResolver(resolver, 10);
        var reference = mode switch
        {
            "stream" => await resolver.UploadAsync(source, "instance", TestToken),
            "chunks" => await resolver.UploadAsync(Chunks(), "instance", TestToken),
            _ => await resolver.UploadAsync((stream, ct) => stream.WriteAsync(expected, ct).AsTask(), "instance", TestToken)
        };
        Assert.Equal(3, reference.Size);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(expected)).ToLowerInvariant(), reference.ContentHash);
        Assert.StartsWith("workflow-payloads/instance/uploads/", reference.Key);
        Assert.True(source.CanRead);
        if (mode == "chunks")
        {
            Assert.Equal(1, enumerations);
            Assert.True(disposed);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChunkUploadDisposesEnumerationOnFailureOrCancellation(bool cancel)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestToken);
        var failure = new IOException("Producer failed.");
        var disposed = false;
        async IAsyncEnumerable<ReadOnlyMemory<byte>> Chunks([EnumeratorCancellation] CancellationToken ct = default)
        {
            try
            {
                yield return new byte[] { 1 };
                if (!cancel)
                    throw failure;
                await cancellation.CancelAsync();
                await Task.Delay(Timeout.Infinite, ct);
            }
            finally { disposed = true; }
        }
        var store = UploadStore((stream, ct) => stream.CopyToAsync(Stream.Null, ct));
        store.Setup(s => s.DeleteBinaryFileAsync("store", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var upload = CreateResolver(store).UploadAsync(Chunks(TestToken), "instance", cancellation.Token);
        if (cancel)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => upload.WaitAsync(TimeSpan.FromSeconds(5), TestToken));
        else
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => upload));
        Assert.True(disposed);
        store.VerifyAll();
    }

    [Fact]
    public async Task UploadAdaptersValidateWithoutStartingTheStore()
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var resolver = CreateResolver(store);
        using var source = new MemoryStream();
        source.Dispose();
        await Assert.ThrowsAsync<ArgumentNullException>(() => resolver.UploadAsync((Stream)null!, "instance", TestToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            resolver.UploadAsync((IAsyncEnumerable<ReadOnlyMemory<byte>>)null!, "instance", TestToken));
        await Assert.ThrowsAsync<ArgumentException>(() => resolver.UploadAsync(source, "instance", TestToken));
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UploadAdaptersRemainUnavailableToOrchestrators()
    {
        var inner = new Mock<IWorkflowPayloadResolver>(MockBehavior.Strict);
        var handler = new WorkflowPayloadHandler(new JsonDaprSerializer(), new DefaultWorkflowPayloadConverter());
        IWorkflowPayloadResolver queued = new QueuedWorkflowPayloadResolver(inner.Object, handler);
        using var source = new MemoryStream(new byte[] { 1 });
        var chunks = new Mock<IAsyncEnumerable<ReadOnlyMemory<byte>>>(MockBehavior.Strict);
        await Assert.ThrowsAsync<InvalidOperationException>(() => queued.UploadAsync(source, "instance", TestToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => queued.UploadAsync(chunks.Object, "instance", TestToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => queued.UploadAsync(
            (_, _) => throw new Exception("Producer must not run."), "instance", TestToken));
        Assert.Equal(0, source.Position);
        chunks.VerifyNoOtherCalls();
        inner.VerifyNoOtherCalls();
        Assert.DoesNotContain(typeof(WorkflowContextPayloadExtensions).GetMethods(),
            m => m.Name.StartsWith("Upload", StringComparison.Ordinal) || m.Name.StartsWith("Write", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1024 * 1024 + 17)]
    public async Task WritesExactBytesHashAndSizeBeforeRecordingReference(int size)
    {
        var bytes = new byte[size];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)(i % 251);
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var manifest = new Mock<IWorkflowPayloadManifest>(MockBehavior.Strict);
        var uploaded = false;
        Stream? destination = null;
        string? key = null;
        store.Setup(s => s.SetBinaryFileAsync("store", It.IsAny<string>(), It.IsAny<Stream>(), false,
                DaprBinaryStoreClient.DefaultChunkSize, It.IsAny<CancellationToken>()))
            .Returns(async (string _, string name, Stream content, bool _, int _, CancellationToken ct) =>
            {
                key = name;
                Assert.False(content.CanSeek);
                Assert.IsNotType<FileStream>(content);
                using var copy = new MemoryStream();
                await content.CopyToAsync(copy, ct);
                Assert.Equal(bytes, copy.ToArray());
                uploaded = true;
            });
        PayloadReference? recorded = null;
        manifest.Setup(m => m.RecordAsync("instance", It.IsAny<PayloadReference>(), TestToken))
            .Returns((string _, PayloadReference reference, CancellationToken _) =>
            {
                Assert.True(uploaded);
                recorded = reference;
                return Task.CompletedTask;
            });

        var result = await CreateResolver(store, manifest.Object).WriteAsync(async (stream, ct) =>
        {
            destination = stream;
            Assert.True(stream.CanWrite);
            Assert.False(stream.CanRead);
            Assert.False(stream.CanSeek);
            Assert.Throws<NotSupportedException>(() => stream.Position = 0);
            Assert.Throws<NotSupportedException>(() => stream.ReadByte());
            await stream.WriteAsync(bytes, ct);
        }, "instance", TestToken);

        Assert.Same(result, recorded);
        Assert.Equal(size, result.Size);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), result.ContentHash);
        Assert.Equal(key, result.Key);
        Assert.StartsWith("workflow-payloads/instance/uploads/", result.Key);
        Assert.Empty(result.Stages);
        Assert.True(PayloadReference.TryParse(result.ToEnvelope(), out var parsed));
        Assert.Equal(result.ToEnvelope(), parsed!.ToEnvelope());
        Assert.False(destination!.CanWrite);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => destination.WriteAsync(bytes, TestToken).AsTask());
        store.VerifyAll();
        manifest.VerifyAll();
    }

    [Fact]
    public async Task UploadReceivesFirstPageBeforeProducerFetchesTheNextPage()
    {
        var firstPageReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var producerFinished = false;
        var store = UploadStore(async (source, ct) =>
        {
            var first = new byte[3];
            await source.ReadExactlyAsync(first, ct);
            Assert.Equal(new byte[] { 1, 2, 3 }, first);
            Assert.False(producerFinished);
            firstPageReceived.SetResult();
            using var remaining = new MemoryStream();
            await source.CopyToAsync(remaining, ct);
            Assert.Equal(new byte[] { 4, 5 }, remaining.ToArray());
        });

        var reference = await CreateResolver(store).WriteAsync(async (destination, ct) =>
        {
            await destination.WriteAsync(new byte[] { 1, 2, 3 }, ct);
            await firstPageReceived.Task.WaitAsync(ct);
            await destination.WriteAsync(new byte[] { 4, 5 }, ct);
            producerFinished = true;
        }, "instance", TestToken).WaitAsync(TimeSpan.FromSeconds(10), TestToken);

        Assert.Equal(5, reference.Size);
        store.VerifyAll();
    }

    [Fact]
    public async Task LargeSingleWriteAppliesBackpressureUntilUploadReads()
    {
        var allowReading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writeStarted = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = UploadStore(async (source, ct) =>
        {
            await allowReading.Task.WaitAsync(ct);
            // Even a multi-megabyte caller write must only publish a bounded slice before pausing.
            var read = await source.ReadAsync(new byte[4 * 1024 * 1024], ct);
            Assert.Equal(WorkflowPayloadUpload.BufferSize, read);
            await source.CopyToAsync(Stream.Null, ct);
        });
        var uploading = CreateResolver(store).WriteAsync(async (destination, ct) =>
        {
            var write = destination.WriteAsync(new byte[4 * 1024 * 1024], ct).AsTask();
            writeStarted.SetResult(write);
            await write;
        }, "instance", TestToken);

        var pendingWrite = await writeStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestToken);
        try
        {
            Assert.False(pendingWrite.IsCompleted);
            Assert.False(uploading.IsCompleted);
        }
        finally
        {
            allowReading.TrySetResult();
        }
        Assert.Equal(4 * 1024 * 1024, (await uploading.WaitAsync(TimeSpan.FromSeconds(10), TestToken)).Size);
    }

    [Fact]
    public async Task SyncWritersAndSourceCopiesWorkWithoutTakingSourceOwnership()
    {
        var data = Encoding.UTF8.GetBytes("prefix:payload");
        using var source = new MemoryStream(data);
        source.Position = 7;
        var store = UploadStore(async (content, ct) =>
        {
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, ct);
            Assert.Equal("payload!", Encoding.UTF8.GetString(copy.ToArray()));
        });
        var result = await CreateResolver(store).WriteAsync(async (destination, ct) =>
        {
            await source.CopyToAsync(destination, ct);
            using var writer = new StreamWriter(destination, new UTF8Encoding(false), leaveOpen: true);
            writer.Write('!');
            writer.Flush();
        }, "instance", TestToken);
        Assert.True(source.CanRead);
        Assert.Equal(data.Length, source.Position);
        Assert.Equal(8, result.Size);
    }

    [Theory]
    [InlineData(PayloadWritePreference.Preserve)]
    [InlineData(PayloadWritePreference.PreferInline)]
    [InlineData(PayloadWritePreference.PreferReference)]
    public async Task ExplicitWritesIgnorePolicyAndUseDistinctKeysForIdenticalContent(PayloadWritePreference preference)
    {
        var store = UploadStore((source, ct) => source.CopyToAsync(Stream.Null, ct));
        var resolver = CreateResolver(store, preference: preference);
        Task Write(Stream stream, CancellationToken ct) => stream.WriteAsync(new byte[] { 1, 2 }, ct).AsTask();
        var first = await resolver.WriteAsync(Write, "instance", TestToken);
        var second = await resolver.WriteAsync(Write, "instance", TestToken);
        Assert.NotEqual(first.Key, second.Key);
        Assert.Equal(first.ContentHash, second.ContentHash);
        Assert.Equal(first.Size, second.Size);
    }

    [Theory]
    [InlineData("producer")]
    [InlineData("upload")]
    [InlineData("manifest")]
    public async Task FailureCancelsOtherSideAndDeletesOnlyTheNewKey(string phase)
    {
        var failure = new IOException(phase);
        var producerWaiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var producerExited = false;
        var store = UploadStore(async (source, ct) =>
        {
            if (phase == "upload")
            {
                await producerWaiting.Task.WaitAsync(ct);
                throw failure;
            }
            await source.CopyToAsync(Stream.Null, ct);
        });
        string? deletedKey = null;
        store.Setup(s => s.DeleteBinaryFileAsync("store", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string key, CancellationToken ct) =>
            {
                Assert.True(producerExited);
                Assert.False(ct.IsCancellationRequested);
                deletedKey = key;
                return Task.CompletedTask;
            });
        var manifest = new Mock<IWorkflowPayloadManifest>(MockBehavior.Strict);
        if (phase == "manifest")
            manifest.Setup(m => m.RecordAsync("instance", It.IsAny<PayloadReference>(), TestToken)).ThrowsAsync(failure);

        var error = await Assert.ThrowsAsync<IOException>(() => CreateResolver(store, manifest.Object).WriteAsync(
            async (destination, ct) =>
            {
                try
                {
                    if (phase == "producer")
                    {
                        await destination.WriteAsync(new byte[] { 1, 2, 3 }, ct);
                        throw failure;
                    }
                    if (phase == "upload")
                    {
                        producerWaiting.SetResult();
                        // Deliberately omit the token: the destination must still honor operation cancellation.
                        await destination.WriteAsync(new byte[4 * 1024 * 1024]);
                    }
                    else
                        await destination.WriteAsync(new byte[] { 1 }, ct);
                }
                finally { producerExited = true; }
            }, "instance", TestToken).WaitAsync(TimeSpan.FromSeconds(10), TestToken));

        Assert.Same(failure, error);
        Assert.StartsWith("workflow-payloads/instance/uploads/", deletedKey);
        var uploadKey = (string)store.Invocations.Single(i => i.Method.Name == nameof(DaprBinaryStoreClient.SetBinaryFileAsync)).Arguments[1];
        Assert.Equal(uploadKey, deletedKey);
        store.VerifyAll();
        manifest.VerifyAll();
        if (phase != "manifest")
            manifest.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CancellationUnblocksProducerAndUsesIndependentCleanupToken()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestToken);
        var writing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = UploadStore((_, ct) => Task.Delay(Timeout.Infinite, ct));
        store.Setup(s => s.DeleteBinaryFileAsync("store", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string _, CancellationToken ct) =>
            {
                Assert.False(ct.IsCancellationRequested);
                return Task.CompletedTask;
            });
        var uploading = CreateResolver(store).WriteAsync(async (destination, _) =>
        {
            var pending = destination.WriteAsync(new byte[1024 * 1024]).AsTask();
            writing.SetResult();
            await pending;
        }, "instance", cancellation.Token);
        await writing.Task.WaitAsync(TimeSpan.FromSeconds(10), TestToken);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => uploading.WaitAsync(TimeSpan.FromSeconds(10), TestToken));
        store.VerifyAll();
    }

    [Fact]
    public async Task CleanupFailureReportsBothErrors()
    {
        var failure = new IOException("upload");
        var cleanupFailure = new IOException("cleanup");
        var store = UploadStore((_, _) => Task.FromException(failure));
        store.Setup(s => s.DeleteBinaryFileAsync("store", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(cleanupFailure);
        var error = await Assert.ThrowsAsync<AggregateException>(() => CreateResolver(store).WriteAsync(
            (_, _) => Task.CompletedTask, "instance", TestToken));
        Assert.Equal(new Exception[] { failure, cleanupFailure }, error.InnerExceptions);
    }

    [Fact]
    public async Task MissingCleanupObjectPreservesOriginalError()
    {
        var failure = new IOException("upload");
        var store = UploadStore((_, _) => Task.FromException(failure));
        store.Setup(s => s.DeleteBinaryFileAsync("store", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DaprException("missing", new RpcException(new Status(StatusCode.NotFound, "missing"))));
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => CreateResolver(store).WriteAsync(
            (_, _) => Task.CompletedTask, "instance", TestToken)));
    }

    [Fact]
    public async Task CollisionIsNeitherDeduplicatedNorDeleted()
    {
        var failure = new DaprException("exists", new RpcException(new Status(StatusCode.AlreadyExists, "exists")));
        var store = UploadStore((_, _) => Task.FromException(failure));
        Assert.Same(failure, await Assert.ThrowsAsync<DaprException>(() => CreateResolver(store).WriteAsync(
            (_, _) => Task.CompletedTask, "instance", TestToken)));
        store.Verify(s => s.DeleteBinaryFileAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StoreMustConsumeThroughEndOfStream()
    {
        var store = UploadStore((_, _) => Task.CompletedTask);
        store.Setup(s => s.DeleteBinaryFileAsync("store", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        await Assert.ThrowsAsync<IOException>(() => CreateResolver(store).WriteAsync(
            (stream, ct) => stream.WriteAsync(new byte[1024 * 1024], ct).AsTask(), "instance", TestToken)
            .WaitAsync(TimeSpan.FromSeconds(10), TestToken));
    }

    [Fact]
    public async Task InvalidArgumentsAndPreCancellationDoNotStartUpload()
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var resolver = CreateResolver(store);
        Task Write(Stream _, CancellationToken __) => throw new InvalidOperationException("Must not be invoked.");
        await Assert.ThrowsAsync<ArgumentNullException>(() => resolver.WriteAsync(null!, "instance", TestToken));
        await Assert.ThrowsAsync<ArgumentException>(() => resolver.WriteAsync(Write, " ", TestToken));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resolver.WriteAsync(Write, "instance", new CancellationToken(true)));
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CacheForwardsWriterButOrchestratorRejectsIt()
    {
        var reference = new PayloadReference("store", "key", 0, "hash");
        Func<Stream, CancellationToken, Task> write = (_, _) => Task.CompletedTask;
        var inner = new Mock<IWorkflowPayloadResolver>(MockBehavior.Strict);
        inner.Setup(r => r.WriteAsync(write, "instance", TestToken)).ReturnsAsync(reference);
        var cached = new CachingWorkflowPayloadResolver(inner.Object, 10);
        Assert.Same(reference, await cached.WriteAsync(write, "instance", TestToken));
        var handler = new WorkflowPayloadHandler(new JsonDaprSerializer(), new DefaultWorkflowPayloadConverter());
        var queued = new QueuedWorkflowPayloadResolver(cached, handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => queued.WriteAsync(write, "instance", TestToken));
        inner.Verify(r => r.WriteAsync(write, "instance", TestToken), Times.Once);
        inner.VerifyNoOtherCalls();
    }

    private static Mock<DaprBinaryStoreClient> UploadStore(Func<Stream, CancellationToken, Task> upload)
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        store.Setup(s => s.SetBinaryFileAsync("store", It.IsAny<string>(), It.IsAny<Stream>(), false,
                DaprBinaryStoreClient.DefaultChunkSize, It.IsAny<CancellationToken>()))
            .Returns((string _, string _, Stream stream, bool _, int _, CancellationToken ct) => upload(stream, ct));
        return store;
    }

    private static IWorkflowPayloadResolver CreateResolver(Mock<DaprBinaryStoreClient> store,
        IWorkflowPayloadManifest? manifest = null, PayloadWritePreference preference = PayloadWritePreference.Preserve)
        => new BinaryStoreWorkflowPayloadResolver(new BinaryStorePayloadConverter(store.Object,
                Options.Create(new BinaryStorePayloadOptions
                {
                    StoreName = "store", WritePreference = preference, OffloadSites = PayloadKind.None,
                    MaxInMemoryRehydrationSize = 1
                }), manifest ?? NullWorkflowPayloadManifest.Instance), new JsonDaprSerializer());
}
