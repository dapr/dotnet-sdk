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
using Dapr.BinaryStore;
using Dapr.Common.Serialization;
using Dapr.Workflow.Payloads;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Moq;

namespace Dapr.Workflow.Test.Payloads;

public sealed class PayloadChunkTests
{
    private static readonly PayloadReference Reference = new("store", "key", 1000, "unchecked-hash");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeChunks_AreLazyUncopiedAndNotCached(bool cache)
    {
        ReadOnlyMemory<byte>[] expected = [new byte[] { 1, 2 }, ReadOnlyMemory<byte>.Empty, new byte[] { 3 }];
        var moves = 0;
        var disposals = 0;
        async IAsyncEnumerable<ReadOnlyMemory<byte>> Download()
        {
            await Task.Yield();
            try
            {
                foreach (var chunk in expected)
                {
                    moves++;
                    yield return chunk;
                }
            }
            finally { disposals++; }
        }
        var store = CreateStore(_ => Download());
        IWorkflowPayloadResolver resolver = CreateResolver(store);
        if (cache)
            resolver = new CachingWorkflowPayloadResolver(resolver, 10);
        var chunks = resolver.ReadChunksAsync(Reference, TestContext.Current.CancellationToken);
        store.VerifyNoOtherCalls();

        for (var pass = 0; pass < 2; pass++)
        {
            var index = 0;
            await foreach (var chunk in chunks)
            {
                Assert.Equal(expected[index++], chunk); // Memory equality also verifies no copy.
                Assert.Equal(pass * expected.Length + index, moves);
            }
            Assert.Equal(expected.Length, index);
            Assert.Equal(pass + 1, disposals);
        }
        store.Verify(s => s.GetBinaryFileChunksAsync("store", "key", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EarlyExit_DisposesWithoutFetchingAnotherChunk(bool useStreamFallback)
    {
        var disposed = false;
        async IAsyncEnumerable<ReadOnlyMemory<byte>> Download()
        {
            await Task.Yield();
            try
            {
                yield return new byte[] { 1, 2 };
                throw new InvalidOperationException("The second chunk must not be fetched.");
            }
            finally { disposed = true; }
        }
        IWorkflowPayloadResolver resolver = CreateResolver(CreateStore(_ => Download()));
        if (useStreamFallback)
            resolver = new StreamOnlyResolver(resolver);
        await foreach (var chunk in resolver.ReadChunksAsync(Reference, TestContext.Current.CancellationToken))
        {
            Assert.Equal(new byte[] { 1, 2 }, chunk.ToArray());
            break;
        }
        Assert.True(disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyPayload_CompletesAndDisposes(bool useStreamFallback)
    {
        var disposed = false;
        async IAsyncEnumerable<ReadOnlyMemory<byte>> Download()
        {
            try { await Task.Yield(); yield break; }
            finally { disposed = true; }
        }
        IWorkflowPayloadResolver resolver = CreateResolver(CreateStore(_ => Download()));
        if (useStreamFallback)
            resolver = new StreamOnlyResolver(resolver);
        await foreach (var _ in resolver.ReadChunksAsync(Reference, TestContext.Current.CancellationToken))
            Assert.Fail("An empty payload must not produce data.");
        Assert.True(disposed);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Cancellation_AbortsPendingDownloadAndDisposes(bool useStreamFallback, bool cancelEnumeratorToken)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposed = false;
        CancellationToken downloadToken = default;
        async IAsyncEnumerable<ReadOnlyMemory<byte>> Download([EnumeratorCancellation] CancellationToken token)
        {
            downloadToken = token;
            try
            {
                yield return new byte[] { 1 };
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            finally { disposed = true; }
        }
        IWorkflowPayloadResolver resolver = CreateResolver(CreateStore(Download));
        if (useStreamFallback)
            resolver = new StreamOnlyResolver(resolver);
        using var methodCancellation = new CancellationTokenSource();
        using var enumerationCancellation = new CancellationTokenSource();
        var iterator = resolver.ReadChunksAsync(Reference, methodCancellation.Token)
            .GetAsyncEnumerator(enumerationCancellation.Token);
        try
        {
            Assert.True(await iterator.MoveNextAsync());
            var next = iterator.MoveNextAsync().AsTask();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await (cancelEnumeratorToken ? enumerationCancellation : methodCancellation).CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                next.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.True(downloadToken.IsCancellationRequested);
        }
        finally
        {
            await methodCancellation.CancelAsync();
            await iterator.DisposeAsync();
        }
        Assert.True(disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidOrCanceledRead_DoesNotOpenStorage(bool useStreamFallback)
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        IWorkflowPayloadResolver resolver = CreateResolver(store);
        if (useStreamFallback)
            resolver = new StreamOnlyResolver(resolver);
        await using var invalid = resolver.ReadChunksAsync(null!, TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ArgumentNullException>(() => invalid.MoveNextAsync().AsTask());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await using var canceled = resolver.ReadChunksAsync(Reference, TestContext.Current.CancellationToken).GetAsyncEnumerator(cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled.MoveNextAsync().AsTask());
        store.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DownloadFailure_DisposesAndTranslatesOnlyNotFound(bool missing, bool afterFirstChunk)
    {
        Exception failure = missing
            ? new DaprException("missing", new RpcException(new Status(StatusCode.NotFound, "missing")))
            : new IOException("download failed");
        var disposed = false;
        async IAsyncEnumerable<ReadOnlyMemory<byte>> Download()
        {
            await Task.Yield();
            try
            {
                if (afterFirstChunk)
                    yield return new byte[] { 1 };
                throw failure;
            }
            finally { disposed = true; }
        }
        var resolver = CreateResolver(CreateStore(_ => Download()));
        await using var iterator = resolver.ReadChunksAsync(Reference, TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);
        if (afterFirstChunk)
            Assert.True(await iterator.MoveNextAsync());
        if (missing)
        {
            var error = await Assert.ThrowsAsync<PayloadNotFoundException>(() => iterator.MoveNextAsync().AsTask());
            Assert.Same(Reference, error.Reference);
            Assert.Same(failure, error.InnerException);
        }
        else
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => iterator.MoveNextAsync().AsTask()));
        Assert.True(disposed);
    }

    [Fact]
    public async Task SynchronousNotFound_IsTranslated()
    {
        var failure = new DaprException("missing", new RpcException(new Status(StatusCode.NotFound, "missing")));
        var resolver = CreateResolver(CreateStore(_ => throw failure));
        await using var iterator = resolver.ReadChunksAsync(Reference, TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<PayloadNotFoundException>(() => iterator.MoveNextAsync().AsTask());
        Assert.Same(failure, error.InnerException);
    }

    [Fact]
    public async Task StreamFallback_ReadsLargePayloadWithBoundedChunks()
    {
        var bytes = Enumerable.Range(0, 150_000).Select(i => (byte)i).ToArray();
        async IAsyncEnumerable<ReadOnlyMemory<byte>> Download()
        {
            await Task.Yield();
            yield return bytes;
        }
        IWorkflowPayloadResolver resolver = new StreamOnlyResolver(CreateResolver(CreateStore(_ => Download())));
        using var copy = new MemoryStream();
        var count = 0;
        await foreach (var chunk in resolver.ReadChunksAsync(Reference, TestContext.Current.CancellationToken))
        {
            Assert.InRange(chunk.Length, 1, 64 * 1024);
            copy.Write(chunk.Span);
            count++;
        }
        Assert.True(count > 1);
        Assert.Equal(bytes, copy.ToArray());
    }

    [Fact]
    public async Task WorkflowChunks_QueueOpeningReadingAndEarlyDisposal()
    {
        // Run without the test synchronization context, as the worker drains inline continuations.
        await Task.Run(async () =>
        {
            var disposed = false;
            async IAsyncEnumerable<ReadOnlyMemory<byte>> Download()
            {
                await Task.Yield();
                try
                {
                    yield return new byte[] { 1 };
                    yield return new byte[] { 2 };
                    throw new InvalidOperationException("Enumeration should have stopped.");
                }
                finally { disposed = true; }
            }
            var store = CreateStore(_ => Download());
            var handler = new WorkflowPayloadHandler(new JsonDaprSerializer(), new DefaultWorkflowPayloadConverter());
            IWorkflowPayloadResolver resolver = new QueuedWorkflowPayloadResolver(CreateResolver(store), handler);
            var context = new Mock<WorkflowContext>();
            context.As<IWorkflowFeatureSource>().Setup(s => s.GetFeature<IWorkflowPayloadResolver>()).Returns(resolver);
            var iterator = context.Object.ReadPayloadChunksAsync(Reference, TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
            var first = iterator.MoveNextAsync().AsTask();
            Assert.False(first.IsCompleted);
            store.VerifyNoOtherCalls();
            await handler.DrainAsync();
            Assert.True(await first.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.Equal(new byte[] { 1 }, iterator.Current.ToArray());
            var second = iterator.MoveNextAsync().AsTask();
            Assert.False(second.IsCompleted);
            await handler.DrainAsync();
            Assert.True(await second.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.Equal(new byte[] { 2 }, iterator.Current.ToArray());
            var disposal = iterator.DisposeAsync().AsTask();
            Assert.False(disposal.IsCompleted);
            Assert.False(disposed);
            await handler.DrainAsync();
            await disposal.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.True(disposed);
        }, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(1, 17)]
    [InlineData(7, 0)]
    [InlineData(7, 1)]
    [InlineData(7, 7)]
    [InlineData(7, 14)]
    [InlineData(7, 17)]
    [InlineData(4 * 1024 * 1024, 8 * 1024 * 1024 + 17)]
    public async Task SizedChunks_FillSplitAndRepeatWithoutCaching(int chunkSize, int payloadLength)
    {
        var bytes = new byte[payloadLength];
        new Random(42).NextBytes(bytes);
        foreach (var useStreamFallback in new[] { false, true })
        foreach (var cache in new[] { false, true })
        {
            var disposals = 0;
            async IAsyncEnumerable<ReadOnlyMemory<byte>> Download()
            {
                await Task.Yield();
                try
                {
                    yield return ReadOnlyMemory<byte>.Empty;
                    var offset = 0;
                    int[] sizes = [1, 17, chunkSize + 3];
                    for (var index = 0; offset < bytes.Length; index++)
                    {
                        var length = Math.Min(sizes[index % sizes.Length], bytes.Length - offset);
                        yield return bytes.AsMemory(offset, length);
                        offset += length;
                    }
                }
                finally { disposals++; }
            }

            var store = CreateStore(_ => Download());
            IWorkflowPayloadResolver resolver = CreateResolver(store);
            if (useStreamFallback)
                resolver = new StreamOnlyResolver(resolver);
            if (cache)
                resolver = new CachingWorkflowPayloadResolver(resolver, 10);
            var chunks = resolver.ReadChunksAsync(Reference, chunkSize, TestContext.Current.CancellationToken);
            store.VerifyNoOtherCalls();
            for (var pass = 0; pass < 2; pass++)
            {
                var offset = 0;
                await foreach (var chunk in chunks)
                {
                    Assert.Equal(Math.Min(chunkSize, bytes.Length - offset), chunk.Length);
                    Assert.True(chunk.Span.SequenceEqual(bytes.AsSpan(offset, chunk.Length)));
                    offset += chunk.Length;
                }
                Assert.Equal(bytes.Length, offset);
                Assert.Equal(pass + 1, disposals);
            }
            store.Verify(s => s.GetBinaryFileChunksAsync("store", "key", It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task SizedChunks_InvalidSizeDoesNotOpenStorage(int chunkSize)
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var resolver = CreateResolver(store);
        await using var iterator = resolver.ReadChunksAsync(Reference, chunkSize, TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => iterator.MoveNextAsync().AsTask());
        Assert.Equal("chunkSize", error.ParamName);
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SizedChunks_NullOrCanceledReadDoesNotOpenStorage()
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var resolver = CreateResolver(store);
        await using var invalid = resolver.ReadChunksAsync(null!, 7, TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ArgumentNullException>(() => invalid.MoveNextAsync().AsTask());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await using var canceled = resolver.ReadChunksAsync(Reference, 7, TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled.MoveNextAsync().AsTask());
        store.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SizedChunks_EarlyExitOrConsumerFailureDisposesWithoutReadingAhead(bool throwFromConsumer)
    {
        var disposed = false;
        var failure = new InvalidOperationException("Consumer failed.");
        async IAsyncEnumerable<ReadOnlyMemory<byte>> Download()
        {
            await Task.Yield();
            try
            {
                yield return new byte[] { 1, 2, 3, 4 };
                throw new InvalidOperationException("The next storage chunk must not be fetched.");
            }
            finally { disposed = true; }
        }
        var resolver = CreateResolver(CreateStore(_ => Download()));
        async Task Consume()
        {
            await foreach (var chunk in resolver.ReadChunksAsync(Reference, 2, TestContext.Current.CancellationToken))
            {
                Assert.Equal(new byte[] { 1, 2 }, chunk.ToArray());
                if (throwFromConsumer)
                    throw failure;
                break;
            }
        }
        if (throwFromConsumer)
            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(Consume));
        else
            await Consume();
        Assert.True(disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SizedChunks_CancellationWhileFillingDiscardsPartialChunkAndDisposes(bool cancelEnumeratorToken)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposed = false;
        async IAsyncEnumerable<ReadOnlyMemory<byte>> Download([EnumeratorCancellation] CancellationToken token)
        {
            try
            {
                yield return new byte[] { 1 };
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            finally { disposed = true; }
        }
        var resolver = CreateResolver(CreateStore(Download));
        using var methodCancellation = new CancellationTokenSource();
        using var enumerationCancellation = new CancellationTokenSource();
        var iterator = resolver.ReadChunksAsync(Reference, 2, methodCancellation.Token)
            .GetAsyncEnumerator(enumerationCancellation.Token);
        try
        {
            var first = iterator.MoveNextAsync().AsTask();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.False(first.IsCompleted);
            await (cancelEnumeratorToken ? enumerationCancellation : methodCancellation).CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                first.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        }
        finally
        {
            await methodCancellation.CancelAsync();
            await iterator.DisposeAsync();
        }
        Assert.True(disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SizedChunks_DownloadFailureWhileFillingIsNotReturnedAsPartialSuccess(bool missing)
    {
        Exception failure = missing
            ? new DaprException("missing", new RpcException(new Status(StatusCode.NotFound, "missing")))
            : new IOException("download failed");
        var disposed = false;
        async IAsyncEnumerable<ReadOnlyMemory<byte>> Download()
        {
            await Task.Yield();
            try
            {
                yield return new byte[] { 1 };
                throw failure;
            }
            finally { disposed = true; }
        }
        var resolver = CreateResolver(CreateStore(_ => Download()));
        await using var iterator = resolver.ReadChunksAsync(Reference, 2, TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);
        if (missing)
        {
            var error = await Assert.ThrowsAsync<PayloadNotFoundException>(() => iterator.MoveNextAsync().AsTask());
            Assert.Same(failure, error.InnerException);
        }
        else
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => iterator.MoveNextAsync().AsTask()));
        Assert.True(disposed);
    }

    [Fact]
    public void SizedChunks_ContextAndCacheForwardCustomImplementation()
    {
        var token = TestContext.Current.CancellationToken;
        var chunks = new Mock<IAsyncEnumerable<ReadOnlyMemory<byte>>>(MockBehavior.Strict).Object;
        var inner = new Mock<IWorkflowPayloadResolver>(MockBehavior.Strict);
        inner.Setup(r => r.ReadChunksAsync(Reference, 7, token)).Returns(chunks);
        var context = new Mock<WorkflowContext>();
        context.As<IWorkflowFeatureSource>().Setup(s => s.GetFeature<IWorkflowPayloadResolver>())
            .Returns(new CachingWorkflowPayloadResolver(inner.Object, 10));

        Assert.Same(chunks, context.Object.ReadPayloadChunksAsync(Reference, chunkSize: 7, cancellationToken: token));
        inner.Verify(r => r.ReadChunksAsync(Reference, 7, token), Times.Once);
        inner.VerifyNoOtherCalls();
    }

    [Fact]
    public void SizedChunks_ContextWithoutPayloadSupportThrows()
    {
        var context = new Mock<WorkflowContext>();
        Assert.Throws<InvalidOperationException>(() => context.Object.ReadPayloadChunksAsync(
            Reference, chunkSize: 7, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SizedWorkflowChunks_QueueMultipleReadsPerChunkAndEarlyDisposal()
    {
        await Task.Run(async () =>
        {
            var disposed = false;
            async IAsyncEnumerable<ReadOnlyMemory<byte>> Download()
            {
                await Task.Yield();
                try
                {
                    for (byte value = 1; value <= 4; value++)
                        yield return new byte[] { value };
                    throw new InvalidOperationException("Enumeration should have stopped.");
                }
                finally { disposed = true; }
            }
            var store = CreateStore(_ => Download());
            var handler = new WorkflowPayloadHandler(new JsonDaprSerializer(), new DefaultWorkflowPayloadConverter());
            IWorkflowPayloadResolver resolver = new QueuedWorkflowPayloadResolver(CreateResolver(store), handler);
            var context = new Mock<WorkflowContext>();
            context.As<IWorkflowFeatureSource>().Setup(s => s.GetFeature<IWorkflowPayloadResolver>()).Returns(resolver);
            var iterator = context.Object.ReadPayloadChunksAsync(Reference, 2, TestContext.Current.CancellationToken)
                .GetAsyncEnumerator(TestContext.Current.CancellationToken);
            var first = iterator.MoveNextAsync().AsTask();
            Assert.False(first.IsCompleted);
            store.VerifyNoOtherCalls();
            await handler.DrainAsync();
            Assert.True(await first.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.Equal(new byte[] { 1, 2 }, iterator.Current.ToArray());
            var second = iterator.MoveNextAsync().AsTask();
            Assert.False(second.IsCompleted);
            await handler.DrainAsync();
            Assert.True(await second.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.Equal(new byte[] { 3, 4 }, iterator.Current.ToArray());
            var disposal = iterator.DisposeAsync().AsTask();
            Assert.False(disposal.IsCompleted);
            Assert.False(disposed);
            await handler.DrainAsync();
            await disposal.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.True(disposed);
        }, TestContext.Current.CancellationToken);
    }

    private static Mock<DaprBinaryStoreClient> CreateStore(Func<CancellationToken, IAsyncEnumerable<ReadOnlyMemory<byte>>> download)
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        store.Setup(s => s.GetBinaryFileChunksAsync("store", "key", It.IsAny<CancellationToken>()))
            .Returns((string _, string _, CancellationToken token) => download(token));
        return store;
    }

    private static IWorkflowPayloadResolver CreateResolver(Mock<DaprBinaryStoreClient> store)
        => new BinaryStoreWorkflowPayloadResolver(new BinaryStorePayloadConverter(store.Object,
            Options.Create(new BinaryStorePayloadOptions { StoreName = "store", MaxInMemoryRehydrationSize = 1 }),
            NullWorkflowPayloadManifest.Instance), new JsonDaprSerializer());

    // Implements only the original interface members, exercising source compatibility and the default method.
    private sealed class StreamOnlyResolver(IWorkflowPayloadResolver inner) : IWorkflowPayloadResolver
    {
        public Task<T> ResolveAsync<T>(PayloadReference reference, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<Stream> OpenAsync(PayloadReference reference, CancellationToken cancellationToken = default)
            => inner.OpenAsync(reference, cancellationToken);
        public Task<PayloadReference<T>> StoreAsync<T>(T value, string instanceId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
