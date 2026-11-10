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
using System.Text;
using Dapr.BinaryStore;
using Dapr.Common.Serialization;
using Dapr.Workflow.Client;
using Dapr.Workflow.Payloads;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using grpc = Dapr.DurableTask.Protobuf;

namespace Dapr.Workflow.Test.Payloads;

public sealed class PayloadStreamStorageTests
{
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 1, true)]
    [InlineData(1024 * 1024 + 1, 2, false)]
    public async Task SchedulingAutomaticallyUploadsStreamBeforeSendingReference(int length, int overload, bool suppliedId)
    {
        var token = overload == 2 ? TestContext.Current.CancellationToken : CancellationToken.None;
        using var source = new GeneratedStream(length);
        var serializer = new Mock<IDaprSerializer>(MockBehavior.Strict);
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var grpcClient = new Mock<grpc.TaskHubSidecarService.TaskHubSidecarServiceClient>(MockBehavior.Strict);
        var manifest = new Mock<IWorkflowPayloadManifest>(MockBehavior.Strict);
        string? uploadedKey = null;
        string? uploadedHash = null;
        FileStream? staged = null;
        PayloadReference? recorded = null;
        string? recordedInstanceId = null;
        store.Setup(s => s.SetBinaryFileAsync("store", It.IsAny<string>(), It.IsAny<Stream>(), false,
                DaprBinaryStoreClient.DefaultChunkSize, token))
            .Returns(async (string _, string key, Stream content, bool _, int _, CancellationToken cancellation) =>
            {
                staged = Assert.IsType<FileStream>(content);
                Assert.Equal(length, content.Length);
                uploadedHash = Convert.ToHexString(await SHA256.HashDataAsync(content, cancellation)).ToLowerInvariant();
                uploadedKey = key;
            });
        manifest.Setup(m => m.RecordAsync(It.IsAny<string>(), It.IsAny<PayloadReference>(), token))
            .Returns((string id, PayloadReference reference, CancellationToken _) =>
            {
                Assert.NotNull(uploadedKey);
                recordedInstanceId = id;
                recorded = reference;
                return Task.CompletedTask;
            });
        var startAt = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        grpcClient.Setup(c => c.StartInstanceAsync(It.IsAny<grpc.CreateInstanceRequest>(), It.IsAny<CallOptions>()))
            .Returns((grpc.CreateInstanceRequest request, CallOptions options) =>
            {
                Assert.NotNull(recorded);
                Assert.Equal(recordedInstanceId, request.InstanceId);
                Assert.Equal(suppliedId ? "instance" : recordedInstanceId, request.InstanceId);
                Assert.False(string.IsNullOrWhiteSpace(request.InstanceId));
                Assert.Equal("workflow", request.Name);
                Assert.Equal(recorded.ToEnvelope(), request.Input);
                Assert.Equal($"workflow-payloads/{request.InstanceId}/{uploadedHash}", uploadedKey);
                Assert.Equal(length, recorded.Size);
                Assert.Equal(token, options.CancellationToken);
                Assert.False(staged!.CanRead);
                if (overload == 0)
                    Assert.Null(request.ScheduledStartTimestamp);
                else
                    Assert.Equal(startAt, request.ScheduledStartTimestamp.ToDateTimeOffset());
                return UnaryCall(Task.FromResult(new grpc.CreateInstanceResponse { InstanceId = request.InstanceId }));
            });
        var codec = new BinaryStorePayloadConverter(store.Object, Options.Create(new BinaryStorePayloadOptions
        {
            StoreName = "store",
            WritePreference = PayloadWritePreference.PreferReference,
            OffloadSites = PayloadKind.WorkflowInput,
            OffloadThreshold = int.MaxValue,
            MaxInMemoryRehydrationSize = 1
        }), manifest.Object);
        var client = new DaprWorkflowClient(new WorkflowGrpcClient(grpcClient.Object,
            NullLogger<WorkflowGrpcClient>.Instance, serializer.Object, payloadConverter: codec));
        var id = suppliedId ? "instance" : null;

        var result = overload switch
        {
            0 => await client.ScheduleNewWorkflowAsync("workflow", id, source),
            1 => await client.ScheduleNewWorkflowAsync("workflow", id, source, startAt.UtcDateTime),
            _ => await client.ScheduleNewWorkflowAsync("workflow", id, source, startAt, token)
        };

        Assert.Equal(recordedInstanceId, result);
        Assert.Equal(length, source.BytesRead);
        Assert.InRange(source.LargestReadRequest, 1, 64 * 1024);
        Assert.False(source.Disposed);
        Assert.False(File.Exists(staged!.Name));
        serializer.VerifyNoOtherCalls();
        store.VerifyAll();
        manifest.VerifyAll();
        grpcClient.VerifyAll();
    }

    [Theory]
    [InlineData(null, PayloadKind.All)]
    [InlineData(PayloadWritePreference.Preserve, PayloadKind.All)]
    [InlineData(PayloadWritePreference.PreferInline, PayloadKind.All)]
    [InlineData(PayloadWritePreference.PreferReference, PayloadKind.None)]
    [InlineData(PayloadWritePreference.PreferReference, PayloadKind.ActivityInput)]
    public async Task SchedulingRejectsStreamWithoutAutomaticInputPolicyBeforeReading(
        PayloadWritePreference? preference, PayloadKind sites)
    {
        using var source = new GeneratedStream(100);
        var serializer = new Mock<IDaprSerializer>(MockBehavior.Strict);
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var grpcClient = new Mock<grpc.TaskHubSidecarService.TaskHubSidecarServiceClient>(MockBehavior.Strict);
        var codec = preference is null ? null : new BinaryStorePayloadConverter(store.Object,
            Options.Create(new BinaryStorePayloadOptions
            {
                StoreName = "store", WritePreference = preference.Value, OffloadSites = sites
            }), NullWorkflowPayloadManifest.Instance);
        var client = new WorkflowGrpcClient(grpcClient.Object, NullLogger<WorkflowGrpcClient>.Instance,
            serializer.Object, payloadConverter: codec);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ScheduleNewWorkflowAsync("workflow", source, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("PreferReference", error.Message);
        Assert.Contains("WorkflowInput", error.Message);
        Assert.Equal(0, source.LargestReadRequest);
        Assert.False(source.Disposed);
        serializer.VerifyNoOtherCalls();
        store.VerifyNoOtherCalls();
        grpcClient.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("source")]
    [InlineData("upload")]
    [InlineData("manifest")]
    [InlineData("cancel")]
    [InlineData("cancel-after-upload")]
    [InlineData("schedule")]
    public async Task SchedulingStreamFailureDoesNotSendPrematurelyOrDisposeSource(string phase)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var token = cancellation.Token;
        var failure = new IOException("Stream scheduling failed.");
        using var source = new GeneratedStream(10000) { ReadFailure = phase == "source" ? failure : null };
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var manifest = new Mock<IWorkflowPayloadManifest>(MockBehavior.Strict);
        var grpcClient = new Mock<grpc.TaskHubSidecarService.TaskHubSidecarServiceClient>(MockBehavior.Strict);
        FileStream? staged = null;
        if (phase is not ("source" or "cancel"))
        {
            store.Setup(s => s.SetBinaryFileAsync("store", It.IsAny<string>(), It.IsAny<Stream>(), false,
                    DaprBinaryStoreClient.DefaultChunkSize, token))
                .Returns((string _, string _, Stream content, bool _, int _, CancellationToken _) =>
                {
                    staged = Assert.IsType<FileStream>(content);
                    return phase == "upload" ? Task.FromException(failure) : Task.CompletedTask;
                });
            if (phase != "upload")
            {
                manifest.Setup(m => m.RecordAsync(It.IsAny<string>(), It.IsAny<PayloadReference>(), token))
                    .Returns((string _, PayloadReference _, CancellationToken _) =>
                    {
                        if (phase == "cancel-after-upload")
                            cancellation.Cancel();
                        return phase == "manifest" ? Task.FromException(failure) : Task.CompletedTask;
                    });
            }
        }
        if (phase == "schedule")
        {
            grpcClient.Setup(c => c.StartInstanceAsync(It.IsAny<grpc.CreateInstanceRequest>(), It.IsAny<CallOptions>()))
                .Returns(UnaryCall(Task.FromException<grpc.CreateInstanceResponse>(failure)));
        }
        if (phase == "cancel")
            cancellation.Cancel();
        var codec = new BinaryStorePayloadConverter(store.Object, Options.Create(new BinaryStorePayloadOptions
        {
            StoreName = "store", WritePreference = PayloadWritePreference.PreferReference
        }), manifest.Object);
        var client = new WorkflowGrpcClient(grpcClient.Object, NullLogger<WorkflowGrpcClient>.Instance,
            new Mock<IDaprSerializer>(MockBehavior.Strict).Object, payloadConverter: codec);
        var scheduling = client.ScheduleNewWorkflowAsync("workflow", source, cancellationToken: token);

        if (phase.StartsWith("cancel", StringComparison.Ordinal))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scheduling);
        else
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => scheduling));

        Assert.False(source.Disposed);
        if (staged is not null)
        {
            Assert.False(staged.CanRead);
            Assert.False(File.Exists(staged.Name));
        }
        if (phase != "schedule")
            grpcClient.VerifyNoOtherCalls();
        store.VerifyAll();
        manifest.VerifyAll();
        grpcClient.VerifyAll();
    }

    private static AsyncUnaryCall<T> UnaryCall<T>(Task<T> response) =>
        new(response, Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(131075)]
    [InlineData(4 * 1024 * 1024 + 1)]
    public async Task NonSeekableSource_UsesBoundedAsyncReadsAndContentAddressing(int length)
    {
        var token = TestContext.Current.CancellationToken;
        using var source = new GeneratedStream(length);
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var manifest = new Mock<IWorkflowPayloadManifest>(MockBehavior.Strict);
        FileStream? staged = null;
        string? uploadedHash = null;
        store.Setup(s => s.SetBinaryFileAsync("store", It.IsAny<string>(), It.IsAny<Stream>(), false,
                DaprBinaryStoreClient.DefaultChunkSize, token))
            .Returns(async (string _, string key, Stream content, bool _, int _, CancellationToken cancellation) =>
            {
                staged = Assert.IsType<FileStream>(content);
                Assert.Equal(length, source.BytesRead);
                Assert.Equal(length, content.Length);
                Assert.Equal(0, content.Position);
                var buffer = new byte[4096];
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                long offset = 0;
                int read;
                while ((read = await content.ReadAsync(buffer, cancellation)) > 0)
                {
                    for (var index = 0; index < read; index++)
                        Assert.Equal((byte)((offset + index) % 251), buffer[index]);
                    hash.AppendData(buffer.AsSpan(0, read));
                    offset += read;
                }
                Assert.Equal(length, offset);
                uploadedHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
                Assert.Equal($"workflow-payloads/instance/{uploadedHash}", key);
            });
        PayloadReference? recorded = null;
        manifest.Setup(m => m.RecordAsync("instance", It.IsAny<PayloadReference>(), token))
            .Returns((string _, PayloadReference reference, CancellationToken _) =>
            {
                Assert.NotNull(uploadedHash);
                recorded = reference;
                return Task.CompletedTask;
            });
        var resolver = CreateResolver(store, manifest.Object);

        var result = await resolver.StoreStreamAsync(source, "instance", token);

        Assert.Equal(length, result.Size);
        Assert.Equal(uploadedHash, result.ContentHash);
        Assert.Same(result, recorded);
        Assert.Empty(result.Stages);
        Assert.Equal(PayloadReference.CurrentVersion, result.Version);
        Assert.True(PayloadReference.TryParse(result.ToEnvelope(), out var parsed));
        Assert.Equal(result.ToEnvelope(), parsed!.ToEnvelope());
        Assert.InRange(source.LargestReadRequest, 1, 64 * 1024);
        Assert.False(source.Disposed);
        Assert.NotNull(staged);
        Assert.False(staged.CanRead);
        Assert.False(File.Exists(staged.Name));
        store.VerifyAll();
        manifest.VerifyAll();
    }

    [Theory]
    [InlineData(PayloadWritePreference.Preserve)]
    [InlineData(PayloadWritePreference.PreferReference)]
    [InlineData(PayloadWritePreference.PreferInline)]
    public async Task StoresRawRemainingBytesRegardlessOfPolicyAndRehydrationLimit(PayloadWritePreference preference)
    {
        var token = TestContext.Current.CancellationToken;
        byte[] bytes = [0, 0xff, 0xfe, 0, 1, 2, 3];
        using var source = new MemoryStream(bytes);
        source.Position = 1;
        var expected = bytes[1..];
        var hash = Convert.ToHexString(SHA256.HashData(expected)).ToLowerInvariant();
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        store.Setup(s => s.SetBinaryFileAsync("store", $"workflow-payloads/instance/{hash}",
                It.IsAny<Stream>(), false, DaprBinaryStoreClient.DefaultChunkSize, token))
            .Returns(async (string _, string _, Stream content, bool _, int _, CancellationToken cancellation) =>
            {
                using var copy = new MemoryStream();
                await content.CopyToAsync(copy, cancellation);
                Assert.Equal(expected, copy.ToArray());
            });
        var serializer = new Mock<IDaprSerializer>(MockBehavior.Strict);
        var codec = new BinaryStorePayloadConverter(store.Object, Options.Create(new BinaryStorePayloadOptions
        {
            StoreName = "store",
            WritePreference = preference,
            OffloadSites = PayloadKind.None,
            OffloadThreshold = int.MaxValue,
            MaxInMemoryRehydrationSize = 1
        }), NullWorkflowPayloadManifest.Instance);
        IWorkflowPayloadResolver resolver = new BinaryStoreWorkflowPayloadResolver(codec, serializer.Object);

        var result = await resolver.StoreStreamAsync(source, "instance", token);

        Assert.Equal(expected.Length, result.Size);
        Assert.Equal(hash, result.ContentHash);
        Assert.Equal(bytes.Length, source.Position);
        Assert.True(source.CanRead);
        serializer.VerifyNoOtherCalls();
        store.VerifyAll();
    }

    [Fact]
    public async Task SerializedStream_ProducesSameReferenceAsTypedStoreAndCanBeResolved()
    {
        var token = TestContext.Current.CancellationToken;
        var bytes = Encoding.UTF8.GetBytes("\"value\"");
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        store.Setup(s => s.SetBinaryFileAsync("store", It.IsAny<string>(), It.IsAny<Stream>(), false,
                DaprBinaryStoreClient.DefaultChunkSize, token))
            .Returns(Task.CompletedTask);
        async IAsyncEnumerable<ReadOnlyMemory<byte>> Download()
        {
            await Task.Yield();
            yield return bytes;
        }
        store.Setup(s => s.GetBinaryFileChunksAsync("store", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Download);
        var resolver = CreateResolver(store);
        using var source = new MemoryStream(bytes);

        var raw = await resolver.StoreStreamAsync(source, "instance", token);
        var typed = await resolver.StoreAsync("value", "instance", token);

        Assert.Equal(typed.ToEnvelope(), raw.ToEnvelope());
        Assert.Equal("value", await resolver.ResolveAsync<string>(raw.AsTyped<string>(), token));
        await using var opened = await resolver.OpenAsync(raw, token);
        using var copy = new MemoryStream();
        await opened.CopyToAsync(copy, token);
        Assert.Equal(bytes, copy.ToArray());
    }

    [Fact]
    public async Task InvalidOrCanceledArguments_DoNotReadOrUpload()
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var resolver = CreateResolver(store);
        using var source = new GeneratedStream(100);
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            resolver.StoreStreamAsync(null!, "instance", TestContext.Current.CancellationToken));
        foreach (var instanceId in new[] { null, "", " " })
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                resolver.StoreStreamAsync(source, instanceId!, TestContext.Current.CancellationToken));
        }
        using var unreadable = new MemoryStream();
        unreadable.Dispose();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            resolver.StoreStreamAsync(unreadable, "instance", TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            resolver.StoreStreamAsync(source, "instance", new CancellationToken(true)));
        Assert.Equal(0, source.LargestReadRequest);
        Assert.False(source.Disposed);
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SourceFailure_IsPropagatedWithoutUploadingOrDisposingSource()
    {
        var failure = new IOException("Source failed after one read.");
        using var source = new GeneratedStream(10000) { ReadFailure = failure };
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var manifest = new Mock<IWorkflowPayloadManifest>(MockBehavior.Strict);
        var resolver = CreateResolver(store, manifest.Object);

        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() =>
            resolver.StoreStreamAsync(source, "instance", TestContext.Current.CancellationToken)));

        Assert.InRange(source.BytesRead, 1, 9999);
        Assert.False(source.Disposed);
        store.VerifyNoOtherCalls();
        manifest.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Cancellation_AbortsPendingSourceReadWithoutUploading()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var source = new GeneratedStream(100) { BlockRead = true };
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var resolver = CreateResolver(store);
        var storing = resolver.StoreStreamAsync(source, "instance", cancellation.Token);

        await source.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            storing.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        Assert.False(source.Disposed);
        store.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task UploadOrManifestFailure_DisposesStagingFile(bool failManifest, bool cancel)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var token = cancellation.Token;
        using var source = new GeneratedStream(100);
        var failure = new IOException("Storage unavailable.");
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var manifest = new Mock<IWorkflowPayloadManifest>(MockBehavior.Strict);
        FileStream? staged = null;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Fail(CancellationToken operationToken)
        {
            entered.SetResult();
            if (cancel)
                await Task.Delay(Timeout.InfiniteTimeSpan, operationToken);
            throw failure;
        }
        store.Setup(s => s.SetBinaryFileAsync("store", It.IsAny<string>(), It.IsAny<Stream>(), false,
                DaprBinaryStoreClient.DefaultChunkSize, token))
            .Returns((string _, string _, Stream content, bool _, int _, CancellationToken operationToken) =>
            {
                staged = Assert.IsType<FileStream>(content);
                return failManifest ? Task.CompletedTask : Fail(operationToken);
            });
        if (failManifest)
        {
            manifest.Setup(m => m.RecordAsync("instance", It.IsAny<PayloadReference>(), token))
                .Returns((string _, PayloadReference _, CancellationToken operationToken) => Fail(operationToken));
        }
        var storing = CreateResolver(store, manifest.Object).StoreStreamAsync(source, "instance", token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        if (cancel)
        {
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                storing.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        }
        else
        {
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => storing));
        }
        Assert.NotNull(staged);
        Assert.False(staged.CanRead);
        Assert.False(File.Exists(staged.Name));
        Assert.False(source.Disposed);
        if (!failManifest)
            manifest.VerifyNoOtherCalls();
        store.VerifyAll();
        manifest.VerifyAll();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AlreadyExists_ObeysSkipPolicyAndRecordsOnlySuccessfulWrites(bool skip)
    {
        var token = TestContext.Current.CancellationToken;
        using var source = new GeneratedStream(100);
        var failure = new DaprException("Exists", new RpcException(new Status(StatusCode.AlreadyExists, "Exists")));
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        FileStream? staged = null;
        store.Setup(s => s.SetBinaryFileAsync("store", It.IsAny<string>(), It.IsAny<Stream>(), false,
                DaprBinaryStoreClient.DefaultChunkSize, token))
            .Returns((string _, string _, Stream content, bool _, int _, CancellationToken _) =>
            {
                staged = Assert.IsType<FileStream>(content);
                return Task.FromException(failure);
            });
        var manifest = new Mock<IWorkflowPayloadManifest>(MockBehavior.Strict);
        if (skip)
        {
            manifest.Setup(m => m.RecordAsync("instance", It.IsAny<PayloadReference>(), token))
                .Returns(Task.CompletedTask);
        }
        var resolver = CreateResolver(store, manifest.Object, skip);
        if (skip)
            Assert.Equal(100, (await resolver.StoreStreamAsync(source, "instance", token)).Size);
        else
            Assert.Same(failure, await Assert.ThrowsAsync<DaprException>(() =>
                resolver.StoreStreamAsync(source, "instance", token)));
        Assert.NotNull(staged);
        Assert.False(File.Exists(staged.Name));
        Assert.False(staged.CanRead);
        Assert.False(source.Disposed);
        store.VerifyAll();
        manifest.VerifyAll();
    }

    [Fact]
    public async Task CacheForwardsStreamWritesButOrchestratorRejectsThem()
    {
        var token = TestContext.Current.CancellationToken;
        using var source = new GeneratedStream(100);
        var reference = new PayloadReference("store", "key", 100, "hash");
        var inner = new Mock<IWorkflowPayloadResolver>(MockBehavior.Strict);
        inner.Setup(r => r.StoreStreamAsync(source, "instance", token)).ReturnsAsync(reference);
        var cached = new CachingWorkflowPayloadResolver(inner.Object, 10);
        Assert.Same(reference, await cached.StoreStreamAsync(source, "instance", token));
        Assert.Same(reference, await cached.StoreStreamAsync(source, "instance", token));
        inner.Verify(r => r.StoreStreamAsync(source, "instance", token), Times.Exactly(2));

        var handler = new WorkflowPayloadHandler(new JsonDaprSerializer(), new DefaultWorkflowPayloadConverter());
        var queued = new QueuedWorkflowPayloadResolver(cached, handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => queued.StoreStreamAsync(source, "instance", token));
        await handler.DrainAsync();
        inner.VerifyNoOtherCalls();
        Assert.Equal(0, source.BytesRead);
    }

    [Fact]
    public async Task ExistingCustomResolvers_FailExplicitlyWithoutReadingTheStream()
    {
        using var source = new GeneratedStream(100);
        IWorkflowPayloadResolver resolver = new LegacyResolver();
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            resolver.StoreStreamAsync(source, "instance", TestContext.Current.CancellationToken));
        Assert.Equal(0, source.BytesRead);
        Assert.False(source.Disposed);
    }

    private static IWorkflowPayloadResolver CreateResolver(Mock<DaprBinaryStoreClient> store,
        IWorkflowPayloadManifest? manifest = null, bool skipWriteIfExists = true)
        => new BinaryStoreWorkflowPayloadResolver(new BinaryStorePayloadConverter(store.Object,
                Options.Create(new BinaryStorePayloadOptions { StoreName = "store", SkipWriteIfExists = skipWriteIfExists }),
                manifest ?? NullWorkflowPayloadManifest.Instance),
            new JsonDaprSerializer());

    private sealed class LegacyResolver : IWorkflowPayloadResolver
    {
        public Task<T> ResolveAsync<T>(PayloadReference reference, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<Stream> OpenAsync(PayloadReference reference, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<PayloadReference<T>> StoreAsync<T>(T value, string instanceId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class GeneratedStream(long length) : Stream
    {
        internal long BytesRead { get; private set; }
        internal int LargestReadRequest { get; private set; }
        internal bool Disposed { get; private set; }
        internal IOException? ReadFailure { get; init; }
        internal bool BlockRead { get; init; }
        internal TaskCompletionSource ReadEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool CanRead => !Disposed;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadEntered.TrySetResult();
            if (BlockRead)
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            if (BytesRead > 0 && ReadFailure is not null)
                throw ReadFailure;
            LargestReadRequest = Math.Max(LargestReadRequest, buffer.Length);
            var read = (int)Math.Min(Math.Min(buffer.Length, 997), length - BytesRead);
            for (var index = 0; index < read; index++)
                buffer.Span[index] = (byte)((BytesRead + index) % 251);
            BytesRead += read;
            return read;
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
