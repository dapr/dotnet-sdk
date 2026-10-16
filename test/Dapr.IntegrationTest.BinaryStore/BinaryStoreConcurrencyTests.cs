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

namespace Dapr.IntegrationTest.BinaryStore;

public sealed partial class BinaryStoreTests
{
    [MinimumDaprRuntimeFact("1.19")]
    public Task ShouldKeepConcurrentFilesAndStreamsIsolatedOnASharedClient() =>
        WithClientAsync(async (client, ct) =>
        {
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var operations = Enumerable.Range(0, 8).Select(async index =>
            {
                var file = $"independent/{index}.bin";
                var data = RandomBytes(64 * 1024 + index * 13, seed: index);
                using var source = new MemoryStream(data);
                await start.Task.WaitAsync(ct);
                await client.SetBinaryFileAsync(StoreName, file, source, chunkSize: 1021 + index,
                    cancellationToken: ct);
                using var destination = new MemoryStream();
                await client.GetBinaryFileAsync(StoreName, file, destination, ct);
                Assert.True(source.CanRead);
                Assert.True(destination.CanWrite);
                Assert.Equal(data, destination.ToArray());
                await client.DeleteBinaryFileAsync(StoreName, file, ct);
                await AssertRpcFailureAsync(StatusCode.NotFound, () =>
                    client.GetBinaryFileBytesAsync(StoreName, file, ct));
            }).ToArray();
            start.SetResult();
            await Task.WhenAll(operations);
        });

    [MinimumDaprRuntimeFact("1.19")]
    public Task ShouldPreserveServerRejectionAndOriginalDuringLargeDuplicateUploads() =>
        WithClientAsync(async (client, ct) =>
        {
            var original = RandomBytes(1024);
            await client.SetBinaryFileAsync(StoreName, "duplicate.bin", original.AsMemory(), cancellationToken: ct);
            // Many small writes exercise rejection while the client is still sending,
            // rather than only observing the final response after a tiny upload.
            for (var attempt = 0; attempt < 8; attempt++)
            {
                using var source = new MemoryStream(RandomBytes(1024 * 1024, seed: attempt));
                await AssertRpcFailureAsync(StatusCode.AlreadyExists, () =>
                    client.SetBinaryFileAsync(StoreName, "duplicate.bin", source, chunkSize: 1024,
                        cancellationToken: ct));
                Assert.Equal(original, await client.GetBinaryFileBytesAsync(StoreName, "duplicate.bin", ct));
            }
        });

    [MinimumDaprRuntimeTheory("1.19")]
    [InlineData(false)]
    [InlineData(true)]
    public Task ShouldNotPublishAnIncompleteUpload(bool overwrite) =>
        WithClientAsync(async (client, ct) =>
        {
            var original = RandomBytes(1024);
            var replacement = RandomBytes(128 * 1024, seed: 43);
            if (overwrite)
                await client.SetBinaryFileAsync(StoreName, "pending.bin", original.AsMemory(), cancellationToken: ct);

            using var source = new EndOfUploadGateStream(replacement);
            var upload = client.SetBinaryFileAsync(StoreName, "pending.bin", source, overwrite, chunkSize: 4096,
                cancellationToken: ct);
            try
            {
                await source.Ready.WaitAsync(ct);
                Assert.False(upload.IsCompleted);
                if (overwrite)
                    Assert.Equal(original, await client.GetBinaryFileBytesAsync(StoreName, "pending.bin", ct));
                else
                    await AssertRpcFailureAsync(StatusCode.NotFound, () =>
                        client.GetBinaryFileBytesAsync(StoreName, "pending.bin", ct));
            }
            finally
            {
                source.Release();
                await upload;
            }
            Assert.Equal(replacement, await client.GetBinaryFileBytesAsync(StoreName, "pending.bin", ct));
        });

    [MinimumDaprRuntimeFact("1.19")]
    public Task ShouldCommitExactlyOneCompetingCreateAndReleaseLoserReservations() =>
        WithClientAsync(async (client, ct) =>
        {
            const int contenderCount = 8;
            const int size = 4096;
            for (var round = 0; round < 4; round++)
            {
                var file = $"contested-{round}.bin";
                var payloads = Enumerable.Range(0, contenderCount)
                    .Select(index => RandomBytes(size, seed: index + round * contenderCount)).ToArray();
                var sources = payloads.Select(data => new EndOfUploadGateStream(data)).ToArray();
                var uploads = sources.Select(source => TryUploadAsync(client, file, source, ct)).ToArray();
                bool[] results;
                try
                {
                    // All bodies have been sent, but none can commit before the shared release.
                    await Task.WhenAll(sources.Select(source => source.Ready)).WaitAsync(ct);
                }
                finally
                {
                    foreach (var source in sources)
                        source.Release();
                    try
                    {
                        results = await Task.WhenAll(uploads);
                    }
                    finally
                    {
                        foreach (var source in sources)
                            source.Dispose();
                    }
                }

                Assert.Single(results, success => success);
                var winner = Array.FindIndex(results, success => success);
                Assert.Equal(payloads[winner], await client.GetBinaryFileBytesAsync(StoreName, file, ct));
                await client.DeleteBinaryFileAsync(StoreName, file, ct);

                // This fills the entire store and detects even one leaked loser reservation.
                var capacity = RandomBytes(contenderCount * size);
                await client.SetBinaryFileAsync(StoreName, "capacity.bin", capacity.AsMemory(), cancellationToken: ct);
                Assert.Equal(capacity, await client.GetBinaryFileBytesAsync(StoreName, "capacity.bin", ct));
                await client.DeleteBinaryFileAsync(StoreName, "capacity.bin", ct);
            }
        }, maxSize: 32768, maxTotalSize: 32768);

    [MinimumDaprRuntimeFact("1.19")]
    public Task ShouldNeverReturnTornDataDuringCompetingOverwritesAndReads() =>
        WithClientAsync(async (client, ct) =>
        {
            var versions = Enumerable.Range(0, 9)
                .Select(index => RandomBytes(128 * 1024 + index * 17, seed: index)).ToArray();
            await client.SetBinaryFileAsync(StoreName, "shared.bin", versions[0].AsMemory(), cancellationToken: ct);

            for (var round = 0; round < 4; round++)
            {
                var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var writers = versions.Skip(1).Select(async data =>
                {
                    await start.Task.WaitAsync(ct);
                    using var source = new MemoryStream(data);
                    await client.SetBinaryFileAsync(StoreName, "shared.bin", source, overwrite: true,
                        chunkSize: 4093, cancellationToken: ct);
                }).ToArray();
                var readers = Enumerable.Range(0, 16).Select(async _ =>
                {
                    await start.Task.WaitAsync(ct);
                    var actual = await client.GetBinaryFileBytesAsync(StoreName, "shared.bin", ct);
                    Assert.Contains(versions, candidate => candidate.AsSpan().SequenceEqual(actual));
                }).ToArray();
                start.SetResult();
                await Task.WhenAll(writers.Concat(readers));
                var final = await client.GetBinaryFileBytesAsync(StoreName, "shared.bin", ct);
                Assert.Contains(versions.Skip(1), candidate => candidate.AsSpan().SequenceEqual(final));
            }
        }, maxSize: 256 * 1024, maxTotalSize: 4 * 1024 * 1024);

    [MinimumDaprRuntimeFact("1.19")]
    public Task ShouldEnforceCapacityUnderConcurrentUploadsWithoutLeakingReservations() =>
        WithClientAsync(async (client, ct) =>
        {
            const int size = 64 * 1024;
            for (var round = 0; round < 4; round++)
            {
                var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var payloads = Enumerable.Range(0, 8).Select(index => RandomBytes(size, seed: index)).ToArray();
                var uploads = payloads.Select(async (data, index) =>
                {
                    await start.Task.WaitAsync(ct);
                    using var source = new MemoryStream(data);
                    return await TryUploadAsync(client, $"quota-{index}.bin", source, ct, StatusCode.ResourceExhausted);
                }).ToArray();
                start.SetResult();
                var results = await Task.WhenAll(uploads);
                // In-flight reservations can cause additional rejections; scheduling must never
                // permit more than two complete files in this two-file store.
                Assert.InRange(results.Count(success => success), 0, 2);
                for (var index = 0; index < results.Length; index++)
                {
                    var file = $"quota-{index}.bin";
                    if (results[index])
                    {
                        Assert.Equal(payloads[index], await client.GetBinaryFileBytesAsync(StoreName, file, ct));
                        await client.DeleteBinaryFileAsync(StoreName, file, ct);
                    }
                    else
                        await AssertRpcFailureAsync(StatusCode.NotFound, () =>
                            client.GetBinaryFileBytesAsync(StoreName, file, ct));
                }
                await client.SetBinaryFileAsync(StoreName, "refill-a.bin", payloads[0].AsMemory(), cancellationToken: ct);
                await client.SetBinaryFileAsync(StoreName, "refill-b.bin", payloads[1].AsMemory(), cancellationToken: ct);
                await AssertRpcFailureAsync(StatusCode.ResourceExhausted, () =>
                    client.SetBinaryFileAsync(StoreName, "overflow.bin", new ReadOnlyMemory<byte>([1]), cancellationToken: ct));
                await client.DeleteBinaryFileAsync(StoreName, "refill-a.bin", ct);
                await client.DeleteBinaryFileAsync(StoreName, "refill-b.bin", ct);
            }
        }, maxSize: 64 * 1024, maxTotalSize: 128 * 1024);

    [MinimumDaprRuntimeFact("1.19")]
    public Task ShouldAllowExactlyOneCompetingDeleteAndReuseTheEntireCapacity() =>
        WithClientAsync(async (client, ct) =>
        {
            var data = RandomBytes(1024);
            for (var round = 0; round < 4; round++)
            {
                await client.SetBinaryFileAsync(StoreName, "delete.bin", data.AsMemory(), cancellationToken: ct);
                var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var deletes = Enumerable.Range(0, 8).Select(async _ =>
                {
                    await start.Task.WaitAsync(ct);
                    try
                    {
                        await client.DeleteBinaryFileAsync(StoreName, "delete.bin", ct);
                        return true;
                    }
                    catch (DaprException error)
                    {
                        Assert.Equal(StatusCode.NotFound, Assert.IsType<RpcException>(error.InnerException).StatusCode);
                        return false;
                    }
                }).ToArray();
                start.SetResult();
                var results = await Task.WhenAll(deletes);
                Assert.Single(results, success => success);
                await AssertRpcFailureAsync(StatusCode.NotFound, () =>
                    client.GetBinaryFileBytesAsync(StoreName, "delete.bin", ct));
            }
            await client.SetBinaryFileAsync(StoreName, "full.bin", data.AsMemory(), cancellationToken: ct);
            // A double decrement during competing deletes would incorrectly allow this byte.
            await AssertRpcFailureAsync(StatusCode.ResourceExhausted, () =>
                client.SetBinaryFileAsync(StoreName, "overflow.bin", new ReadOnlyMemory<byte>([1]), cancellationToken: ct));
            Assert.Equal(data, await client.GetBinaryFileBytesAsync(StoreName, "full.bin", ct));
        }, maxSize: 1024, maxTotalSize: 1024);

    private static async Task<bool> TryUploadAsync(
        DaprBinaryStoreClient client, string file, Stream source, CancellationToken ct,
        StatusCode expectedFailure = StatusCode.AlreadyExists)
    {
        try
        {
            await client.SetBinaryFileAsync(StoreName, file, source, chunkSize: 4096, cancellationToken: ct);
            return true;
        }
        catch (DaprException error)
        {
            Assert.Equal(expectedFailure, Assert.IsType<RpcException>(error.InnerException).StatusCode);
            return false;
        }
    }

    private sealed class EndOfUploadGateStream(byte[] data) : MemoryStream(data, writable: false)
    {
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Ready => ready.Task;

        public void Release() => release.TrySetResult();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position == Length)
            {
                ready.TrySetResult();
                await release.Task.WaitAsync(cancellationToken);
            }
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }
}
