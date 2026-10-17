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

using Dapr.BinaryStore;
using Dapr.Testcontainers.Xunit.Attributes;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.BinaryStore;

public sealed partial class BinaryStoreTests
{
    [MinimumDaprRuntimeTheory("1.19")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1023)]
    [InlineData(1024)]
    [InlineData(1025)]
    public Task ShouldEnforceExactFileSizeBoundaryWithoutLeavingPartialFiles(int length) =>
        WithClientAsync(async (client, ct) =>
        {
            var data = RandomBytes(length);
            if (length <= 1024)
            {
                await client.SetBinaryFileAsync(StoreName, "boundary.bin", data.AsMemory(), cancellationToken: ct);
                Assert.Equal(data, await client.GetBinaryFileBytesAsync(StoreName, "boundary.bin", ct));
            }
            else
            {
                await AssertRpcFailureAsync(StatusCode.ResourceExhausted, () =>
                    client.SetBinaryFileAsync(StoreName, "boundary.bin", data.AsMemory(), cancellationToken: ct));
                await AssertRpcFailureAsync(StatusCode.NotFound, () =>
                    client.GetBinaryFileBytesAsync(StoreName, "boundary.bin", ct));

                // A failed upload must release its reservations, not poison later uploads.
                await client.SetBinaryFileAsync(StoreName, "boundary.bin", RandomBytes(1024).AsMemory(), cancellationToken: ct);
                await client.SetBinaryFileAsync(StoreName, "remaining.bin", RandomBytes(1024).AsMemory(), cancellationToken: ct);
            }
        }, maxSize: 1024, maxTotalSize: 2048);

    [MinimumDaprRuntimeFact("1.19")]
    public Task ShouldEnforceTotalCapacityAndReclaimDeletedBytes() =>
        WithClientAsync(async (client, ct) =>
        {
            var data = RandomBytes(1024);
            await client.SetBinaryFileAsync(StoreName, "first.bin", data.AsMemory(), cancellationToken: ct);
            await client.SetBinaryFileAsync(StoreName, "second.bin", data.AsMemory(), cancellationToken: ct);
            await AssertRpcFailureAsync(StatusCode.ResourceExhausted, () =>
                client.SetBinaryFileAsync(StoreName, "overflow.bin", new ReadOnlyMemory<byte>([1]), cancellationToken: ct));
            await AssertRpcFailureAsync(StatusCode.NotFound, () =>
                client.GetBinaryFileBytesAsync(StoreName, "overflow.bin", ct));
            Assert.Equal(data, await client.GetBinaryFileBytesAsync(StoreName, "first.bin", ct));
            Assert.Equal(data, await client.GetBinaryFileBytesAsync(StoreName, "second.bin", ct));

            await client.DeleteBinaryFileAsync(StoreName, "first.bin", ct);
            await client.SetBinaryFileAsync(StoreName, "replacement.bin", data.AsMemory(), cancellationToken: ct);
            Assert.Equal(data, await client.GetBinaryFileBytesAsync(StoreName, "replacement.bin", ct));
            await AssertRpcFailureAsync(StatusCode.ResourceExhausted, () =>
                client.SetBinaryFileAsync(StoreName, "overflow.bin", new ReadOnlyMemory<byte>([1]), cancellationToken: ct));
        }, maxSize: 1024, maxTotalSize: 2048);

    [MinimumDaprRuntimeFact("1.19")]
    public Task ShouldPreserveOriginalWhenOverwriteExceedsFileLimit() =>
        WithClientAsync(async (client, ct) =>
        {
            var original = RandomBytes(512);
            await client.SetBinaryFileAsync(StoreName, "original.bin", original.AsMemory(), cancellationToken: ct);
            await AssertRpcFailureAsync(StatusCode.ResourceExhausted, () =>
                client.SetBinaryFileAsync(StoreName, "original.bin", RandomBytes(1025).AsMemory(),
                    overwrite: true, cancellationToken: ct));
            Assert.Equal(original, await client.GetBinaryFileBytesAsync(StoreName, "original.bin", ct));

            var replacement = RandomBytes(1024);
            await client.SetBinaryFileAsync(StoreName, "original.bin", replacement.AsMemory(),
                overwrite: true, cancellationToken: ct);
            Assert.Equal(replacement, await client.GetBinaryFileBytesAsync(StoreName, "original.bin", ct));
            await client.SetBinaryFileAsync(StoreName, "remaining.bin", replacement.AsMemory(), cancellationToken: ct);
        }, maxSize: 1024, maxTotalSize: 2048);

    [MinimumDaprRuntimeFact("1.19")]
    public Task ShouldCountOldAndInFlightOverwriteBytesUntilCommit() =>
        WithClientAsync(async (client, ct) =>
        {
            var original = RandomBytes(1024);
            await client.SetBinaryFileAsync(StoreName, "original.bin", original.AsMemory(), cancellationToken: ct);
            await AssertRpcFailureAsync(StatusCode.ResourceExhausted, () =>
                client.SetBinaryFileAsync(StoreName, "original.bin", RandomBytes(1025).AsMemory(),
                    overwrite: true, cancellationToken: ct));
            Assert.Equal(original, await client.GetBinaryFileBytesAsync(StoreName, "original.bin", ct));

            // Equal-sized replacement fits exactly alongside the old allocation.
            await client.SetBinaryFileAsync(StoreName, "original.bin", original.AsMemory(),
                overwrite: true, cancellationToken: ct);
            // Shrinking must reclaim the old allocation after the replacement commits.
            await client.SetBinaryFileAsync(StoreName, "original.bin", new ReadOnlyMemory<byte>([7]),
                overwrite: true, cancellationToken: ct);
            var remaining = RandomBytes(2047);
            await client.SetBinaryFileAsync(StoreName, "remaining.bin", remaining.AsMemory(), cancellationToken: ct);
            Assert.Equal(new byte[] { 7 }, await client.GetBinaryFileBytesAsync(StoreName, "original.bin", ct));
            Assert.Equal(remaining, await client.GetBinaryFileBytesAsync(StoreName, "remaining.bin", ct));
            await AssertRpcFailureAsync(StatusCode.ResourceExhausted, () =>
                client.SetBinaryFileAsync(StoreName, "overflow.bin", new ReadOnlyMemory<byte>([1]), cancellationToken: ct));
        }, maxSize: 2048, maxTotalSize: 2048);

    [MinimumDaprRuntimeFact("1.19")]
    public Task ShouldStreamOrderedChunksWithoutMutatingPreviouslyYieldedMemory() =>
        WithClientAsync(async (client, ct) =>
        {
            var data = RandomBytes(1024 * 1024 + 123);
            using var source = new MemoryStream(data);
            await client.SetBinaryFileAsync(StoreName, "stream.bin", source, chunkSize: 8191, cancellationToken: ct);
            Assert.True(source.CanRead);

            var chunks = new List<ReadOnlyMemory<byte>>();
            var offset = 0;
            await foreach (var chunk in client.GetBinaryFileChunksAsync(StoreName, "stream.bin", ct))
            {
                Assert.False(chunk.IsEmpty);
                Assert.True(offset + chunk.Length <= data.Length);
                Assert.True(data.AsSpan(offset, chunk.Length).SequenceEqual(chunk.Span));
                chunks.Add(chunk);
                offset += chunk.Length;
            }
            Assert.Equal(data.Length, offset);
            Assert.True(chunks.Count > 1);
            Assert.Equal(data, chunks.SelectMany(chunk => chunk.ToArray()).ToArray());
        });

    [MinimumDaprRuntimeFact("1.19")]
    public Task ShouldAllowEarlyEnumerationDisposalAndSubsequentOperations() =>
        WithClientAsync(async (client, ct) =>
        {
            var data = RandomBytes(1024 * 1024);
            await client.SetBinaryFileAsync(StoreName, "stream.bin", data.AsMemory(), cancellationToken: ct);
            for (var attempt = 0; attempt < 8; attempt++)
            {
                await using var reader = client.GetBinaryFileChunksAsync(StoreName, "stream.bin", ct).GetAsyncEnumerator(ct);
                Assert.True(await reader.MoveNextAsync());
                Assert.True(reader.Current.Length < data.Length);
            }
            Assert.Equal(data, await client.GetBinaryFileBytesAsync(StoreName, "stream.bin", ct));
            await client.DeleteBinaryFileAsync(StoreName, "stream.bin", ct);
            await AssertRpcFailureAsync(StatusCode.NotFound, () =>
                client.GetBinaryFileBytesAsync(StoreName, "stream.bin", ct));
        });

    [MinimumDaprRuntimeFact("1.19")]
    public Task ShouldCancelActiveEnumerationWithoutAffectingOtherCalls() =>
        WithClientAsync(async (client, ct) =>
        {
            var data = RandomBytes(1024 * 1024);
            await client.SetBinaryFileAsync(StoreName, "stream.bin", data.AsMemory(), cancellationToken: ct);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
            await using (var reader = client.GetBinaryFileChunksAsync(StoreName, "stream.bin", ct)
                             .WithCancellation(cancellation.Token).GetAsyncEnumerator())
            {
                Assert.True(await reader.MoveNextAsync());
                await cancellation.CancelAsync();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { await reader.MoveNextAsync(); });
            }
            Assert.Equal(data, await client.GetBinaryFileBytesAsync(StoreName, "stream.bin", ct));
        });

    [MinimumDaprRuntimeTheory("1.19")]
    [InlineData(false)]
    [InlineData(true)]
    public Task ShouldRetainReadSnapshotAcrossOverwriteOrDelete(bool delete) =>
        WithClientAsync(async (client, ct) =>
        {
            var original = RandomBytes(1024 * 1024 + 123);
            await client.SetBinaryFileAsync(StoreName, "snapshot.bin", original.AsMemory(), cancellationToken: ct);
            await using var reader = client.GetBinaryFileChunksAsync(StoreName, "snapshot.bin", ct).GetAsyncEnumerator(ct);
            Assert.True(await reader.MoveNextAsync());
            using var received = new MemoryStream();
            await received.WriteAsync(reader.Current, ct);
            Assert.True(received.Length < original.Length);

            var replacement = new byte[] { 9, 8, 7 };
            if (delete)
                await client.DeleteBinaryFileAsync(StoreName, "snapshot.bin", ct);
            else
                await client.SetBinaryFileAsync(StoreName, "snapshot.bin", replacement.AsMemory(),
                    overwrite: true, cancellationToken: ct);

            while (await reader.MoveNextAsync())
                await received.WriteAsync(reader.Current, ct);
            Assert.Equal(original, received.ToArray());

            if (delete)
                await AssertRpcFailureAsync(StatusCode.NotFound, () =>
                    client.GetBinaryFileBytesAsync(StoreName, "snapshot.bin", ct));
            else
                Assert.Equal(replacement, await client.GetBinaryFileBytesAsync(StoreName, "snapshot.bin", ct));
        });

    private static async Task WithClientAsync(
        Func<DaprBinaryStoreClient, CancellationToken, Task> test,
        long? maxSize = null, long? maxTotalSize = null)
    {
        var (environment, app) = await StartAsync(maxSize, maxTotalSize);
        await using var environmentLifetime = environment;
        await using var appLifetime = app;
        using var scope = app.CreateScope();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        await test(scope.ServiceProvider.GetRequiredService<DaprBinaryStoreClient>(), timeout.Token);
    }
}
