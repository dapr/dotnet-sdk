// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using Dapr.BinaryStore;
using Dapr.Workflow.Payloads;
using Microsoft.Extensions.Options;
using Moq;

namespace Dapr.Workflow.Test.Payloads;

public sealed class PayloadStreamCancellationTests
{
    [Fact]
    public async Task ReadCancellation_CancelsDownloadAndDisposesIterator()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken downloadToken = default;
        async IAsyncEnumerable<ReadOnlyMemory<byte>> Download([EnumeratorCancellation] CancellationToken token)
        {
            downloadToken = token;
            try
            {
                yield return new byte[] { 1 };
                entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            finally { disposed.TrySetResult(); }
        }
        var store = new Mock<DaprBinaryStoreClient>();
        store.Setup(s => s.GetBinaryFileChunksAsync("store", "key", It.IsAny<CancellationToken>()))
            .Returns((string _, string _, CancellationToken token) => Download(token));
        var converter = new BinaryStorePayloadConverter(store.Object,
            Options.Create(new BinaryStorePayloadOptions { StoreName = "store" }), new NullWorkflowPayloadManifest());
        await using var stream = await converter.OpenAsync(
            new PayloadReference("store", "key", 1, new string('a', 64)), TestContext.Current.CancellationToken);
        var buffer = new byte[1];
        Assert.Equal(1, await stream.ReadAsync(buffer, TestContext.Current.CancellationToken));
        using var readCancellation = new CancellationTokenSource();
        var read = stream.ReadAsync(buffer, readCancellation.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        await readCancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            read.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));
        Assert.True(downloadToken.IsCancellationRequested);
        await stream.DisposeAsync();
        Assert.True(disposed.Task.IsCompletedSuccessfully);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanceledRead_FencesUncooperativeMoveNext_UntilSafeDisposal(bool endsStream)
    {
        var chunks = new DelayedChunks();
        var stream = new BinaryStorePayloadStream(chunks,
            new PayloadReference("store", "key", 1, new string('a', 64)), true);
        var buffer = new byte[1];
        Assert.Equal(1, await stream.ReadAsync(buffer, TestContext.Current.CancellationToken));
        using var cancellation = new CancellationTokenSource();
        var read = stream.ReadAsync(buffer, cancellation.Token).AsTask();
        await chunks.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                read.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                stream.ReadAsync(buffer, TestContext.Current.CancellationToken).AsTask());
            Assert.Equal(1, chunks.Moves);
            var disposing = stream.DisposeAsync().AsTask();
            Assert.False(disposing.IsCompleted);
            Assert.False(chunks.Disposed);
            chunks.Release.TrySetResult(!endsStream);
            await disposing.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            Assert.True(chunks.Disposed);
        }
        finally
        {
            chunks.Release.TrySetResult(false);
            await stream.DisposeAsync();
        }
    }

    private sealed class DelayedChunks : IAsyncEnumerator<ReadOnlyMemory<byte>>
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Moves { get; private set; }
        public bool Disposed { get; private set; }
        public ReadOnlyMemory<byte> Current => new byte[] { 1 };
        public ValueTask<bool> MoveNextAsync()
        {
            Moves++;
            Entered.TrySetResult();
            return new(Release.Task);
        }
        public ValueTask DisposeAsync()
        {
            Assert.True(Release.Task.IsCompleted);
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
