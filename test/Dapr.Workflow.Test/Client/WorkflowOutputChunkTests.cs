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
using System.Text.Json;
using Dapr.BinaryStore;
using Dapr.Common.Serialization;
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Client;
using Dapr.Workflow.Payloads;
using Dapr.Workflow.Registration;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using GrpcWorkflowState = Dapr.DurableTask.Protobuf.WorkflowState;

namespace Dapr.Workflow.Test.Client;

public sealed class WorkflowOutputChunkTests
{
    private static readonly PayloadReference Reference = new("store", "key", 5, "unchecked-hash");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutputReferenceRetrievalNeverHydratesAndDoesNotRequirePayloadSupport(bool registered)
    {
        var resolver = new Mock<IWorkflowPayloadResolver>(MockBehavior.Strict);
        var grpc = CreateGrpc(Response(Reference.ToEnvelope()));
        IDaprWorkflowClient client = CreateClient(grpc, registered ? resolver.Object : null);
        var reference = await client.GetWorkflowOutputReferenceAsync("instance", TestContext.Current.CancellationToken);
        Assert.Equal(Reference.ToEnvelope(), reference.ToEnvelope());
        grpc.Verify(g => g.GetInstanceAsync(
            It.Is<GetInstanceRequest>(r => r.InstanceId == "instance" && r.GetInputsAndOutputs),
            It.Is<CallOptions>(o => o.CancellationToken == TestContext.Current.CancellationToken &&
                o.Headers != null && o.Headers.Any(h => h.Key == "dapr-api-token" && h.Value == "token"))), Times.Once);
        resolver.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("inline", typeof(InvalidOperationException))]
    [InlineData("missing", typeof(InvalidOperationException))]
    [InlineData("state", typeof(InvalidOperationException))]
    [InlineData("running", typeof(InvalidOperationException))]
    [InlineData("malformed", typeof(FormatException))]
    [InlineData("version", typeof(NotSupportedException))]
    public async Task OutputReferenceRetrievalRejectsUnavailableOrInvalidOutput(string scenario, Type errorType)
    {
        var response = Response(Reference.ToEnvelope());
        switch (scenario)
        {
            case "inline": response.WorkflowState.Output = "\"value\""; break;
            case "missing": response.Exists = false; break;
            case "state": response.WorkflowState = null; break;
            case "running": response.WorkflowState.WorkflowStatus = OrchestrationStatus.Running; break;
            case "malformed": response.WorkflowState.Output = "{\"$daprRef\":{}}"; break;
            case "version": response.WorkflowState.Output = "{\"$daprRef\":{\"v\":99}}"; break;
        }
        var client = CreateClient(CreateGrpc(response), null);
        var error = await Record.ExceptionAsync(() =>
            client.GetWorkflowOutputReferenceAsync("instance", TestContext.Current.CancellationToken));
        Assert.IsType(errorType, error);
    }

    [Theory]
    [InlineData(StatusCode.NotFound)]
    [InlineData(StatusCode.PermissionDenied)]
    [InlineData(StatusCode.Unavailable)]
    public async Task OutputReferenceRetrievalPreservesMetadataFailures(StatusCode status)
    {
        var failure = new RpcException(new Status(status, "metadata failed"));
        var grpc = CreateGrpc();
        grpc.Setup(g => g.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()))
            .Returns(Unary(Task.FromException<GetInstanceResponse>(failure)));
        var client = CreateClient(grpc, null);
        var error = await Record.ExceptionAsync(() =>
            client.GetWorkflowOutputReferenceAsync("instance", TestContext.Current.CancellationToken));
        if (status == StatusCode.NotFound)
            Assert.Same(failure, Assert.IsType<InvalidOperationException>(error).InnerException);
        else
            Assert.Same(failure, error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutputReferenceRetrievalHonorsCancellation(bool alreadyCanceled)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<GetInstanceResponse> Respond(CancellationToken ct)
        {
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("Must be canceled.");
        }
        var grpc = CreateGrpc();
        grpc.Setup(g => g.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()))
            .Returns((GetInstanceRequest _, CallOptions options) => Unary(Respond(options.CancellationToken)));
        if (alreadyCanceled)
            await cancellation.CancelAsync();
        var result = CreateClient(grpc, null).GetWorkflowOutputReferenceAsync("instance", cancellation.Token);
        if (!alreadyCanceled)
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await cancellation.CancelAsync();
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            result.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        if (alreadyCanceled)
            grpc.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null, PayloadWritePreference.Preserve)]
    [InlineData(null, PayloadWritePreference.PreferInline)]
    [InlineData(null, PayloadWritePreference.PreferReference)]
    [InlineData(3, PayloadWritePreference.Preserve)]
    [InlineData(3, PayloadWritePreference.PreferInline)]
    [InlineData(3, PayloadWritePreference.PreferReference)]
    public async Task ReadsAreLazyRepeatableAndNeverHydrateMetadata(int? chunkSize, PayloadWritePreference policy)
    {
        ReadOnlyMemory<byte>[] nativeChunks = [new byte[] { 1, 2 }, new byte[] { 3, 4, 5 }];
        var disposals = 0;
        async IAsyncEnumerable<ReadOnlyMemory<byte>> Download()
        {
            await Task.Yield();
            try
            {
                foreach (var chunk in nativeChunks)
                    yield return chunk;
            }
            finally { disposals++; }
        }

        var grpc = CreateGrpc(Response(Reference.ToEnvelope()));
        var store = CreateStore(_ => Download());
        var client = CreateClient(grpc, CreateResolver(store, policy));
        var chunks = Read(client, chunkSize);
        grpc.VerifyNoOtherCalls();
        store.VerifyNoOtherCalls();
        for (var pass = 0; pass < 2; pass++)
        {
            var offset = 0;
            var index = 0;
            await foreach (var chunk in chunks)
            {
                if (chunkSize is null)
                    Assert.Equal(nativeChunks[index++], chunk);
                else
                    Assert.Equal(Math.Min(chunkSize.Value, 5 - offset), chunk.Length);
                Assert.True(chunk.Span.SequenceEqual(new byte[] { 1, 2, 3, 4, 5 }.AsSpan(offset, chunk.Length)));
                offset += chunk.Length;
            }
            Assert.Equal(5, offset);
            Assert.Equal(pass + 1, disposals);
        }
        grpc.Verify(g => g.GetInstanceAsync(
            It.Is<GetInstanceRequest>(r => r.InstanceId == "instance" && r.GetInputsAndOutputs),
            It.Is<CallOptions>(o => o.Headers != null && o.Headers.Any(h => h.Key == "dapr-api-token" && h.Value == "token"))),
            Times.Exactly(2));
        grpc.VerifyNoOtherCalls();
        store.Verify(s => s.GetBinaryFileChunksAsync("store", "key", It.IsAny<CancellationToken>()), Times.Exactly(2));
        store.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task ConsumerExitDisposesWithoutReadingRest(int? chunkSize, bool consumerThrows)
    {
        var disposed = false;
        async IAsyncEnumerable<ReadOnlyMemory<byte>> Download()
        {
            await Task.Yield();
            try
            {
                yield return new byte[] { 1, 2, 3, 4 };
                throw new InvalidOperationException("The next chunk must not be requested.");
            }
            finally { disposed = true; }
        }
        var client = CreateClient(CreateGrpc(Response(Reference.ToEnvelope())), CreateResolver(CreateStore(_ => Download())));
        var consumerError = new IOException("consumer failed");
        async Task Consume()
        {
            await foreach (var chunk in Read(client, chunkSize))
            {
                Assert.Equal(chunkSize ?? 4, chunk.Length);
                if (consumerThrows)
                    throw consumerError;
                break;
            }
        }
        if (consumerThrows)
            Assert.Same(consumerError, await Assert.ThrowsAsync<IOException>(Consume));
        else
            await Consume();
        Assert.True(disposed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(2)]
    public async Task EmptyReferencedOutputCompletes(int? chunkSize)
    {
        async IAsyncEnumerable<ReadOnlyMemory<byte>> Download()
        {
            await Task.Yield();
            yield break;
        }
        var reference = new PayloadReference("store", "key", 0, "unchecked-hash");
        var client = CreateClient(CreateGrpc(Response(reference.ToEnvelope())), CreateResolver(CreateStore(_ => Download())));
        await foreach (var _ in Read(client, chunkSize))
            Assert.Fail("Empty content should not produce data.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("\"inline\"")]
    [InlineData("{\"value\":42}")]
    public async Task InlineOrAbsentOutputThrowsInsteadOfReturningChunks(string? output)
    {
        var resolver = new Mock<IWorkflowPayloadResolver>(MockBehavior.Strict);
        var client = CreateClient(CreateGrpc(Response(output)), resolver.Object);
        await using var iterator = Read(client, null).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => iterator.MoveNextAsync().AsTask());
        Assert.Contains("inline or absent", error.Message);
        Assert.Contains("ReadOutputAs<T>", error.Message);
        resolver.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(OrchestrationStatus.Pending)]
    [InlineData(OrchestrationStatus.Running)]
    [InlineData(OrchestrationStatus.Suspended)]
    [InlineData(OrchestrationStatus.ContinuedAsNew)]
    [InlineData(OrchestrationStatus.Failed)]
    [InlineData(OrchestrationStatus.Terminated)]
    public async Task NonCompletedWorkflowDoesNotOpenOutput(OrchestrationStatus status)
    {
        var response = Response(Reference.ToEnvelope());
        response.WorkflowState.WorkflowStatus = status;
        var resolver = new Mock<IWorkflowPayloadResolver>(MockBehavior.Strict);
        var client = CreateClient(CreateGrpc(response), resolver.Object);
        await using var iterator = Read(client, null).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => iterator.MoveNextAsync().AsTask());
        Assert.Contains(status.ToString(), error.Message);
        resolver.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingWorkflowOrMissingStateThrows(bool exists)
    {
        var client = CreateClient(CreateGrpc(new GetInstanceResponse { Exists = exists }),
            new Mock<IWorkflowPayloadResolver>(MockBehavior.Strict).Object);
        await using var iterator = Read(client, null).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => iterator.MoveNextAsync().AsTask());
        Assert.Contains(exists ? "returned no state" : "does not exist", error.Message);
    }

    [Fact]
    public async Task MissingPayloadSupportFailsBeforeMetadataLookup()
    {
        var grpc = CreateGrpc();
        var client = CreateClient(grpc, null);
        await using var iterator = Read(client, null).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => iterator.MoveNextAsync().AsTask());
        Assert.Contains("WithBinaryStorePayloads", error.Message);
        grpc.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("{\"$daprRef\":{}}", typeof(FormatException))]
    [InlineData("{\"$daprRef\":{\"v\":99}}", typeof(NotSupportedException))]
    [InlineData("{\"$daprRef\":", typeof(JsonException))]
    public async Task InvalidReferenceErrorsAreNotMasked(string output, Type exceptionType)
    {
        var client = CreateClient(CreateGrpc(Response(output)),
            new Mock<IWorkflowPayloadResolver>(MockBehavior.Strict).Object);
        await using var iterator = Read(client, null).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        var error = await Record.ExceptionAsync(() => iterator.MoveNextAsync().AsTask());
        Assert.IsAssignableFrom(exceptionType, error);
    }

    [Theory]
    [InlineData(StatusCode.NotFound)]
    [InlineData(StatusCode.PermissionDenied)]
    [InlineData(StatusCode.Unavailable)]
    public async Task MetadataFailuresPreserveCause(StatusCode status)
    {
        var failure = new RpcException(new Status(status, "metadata failed"));
        var grpc = CreateGrpc();
        grpc.Setup(g => g.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()))
            .Returns(Unary(Task.FromException<GetInstanceResponse>(failure)));
        var client = CreateClient(grpc, new Mock<IWorkflowPayloadResolver>(MockBehavior.Strict).Object);
        await using var iterator = Read(client, null).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        if (status == StatusCode.NotFound)
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => iterator.MoveNextAsync().AsTask());
            Assert.Same(failure, error.InnerException);
        }
        else
            Assert.Same(failure, await Assert.ThrowsAsync<RpcException>(() => iterator.MoveNextAsync().AsTask()));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task DownloadFailuresPropagateAndDispose(int? chunkSize, bool missing)
    {
        Exception failure = missing
            ? new DaprException("missing", new RpcException(new Status(StatusCode.NotFound, "missing")))
            : new IOException("storage unavailable");
        var disposed = false;
        async IAsyncEnumerable<ReadOnlyMemory<byte>> Download()
        {
            await Task.Yield();
            try
            {
                yield return new byte[] { 1, 2 };
                throw failure;
            }
            finally { disposed = true; }
        }
        var client = CreateClient(CreateGrpc(Response(Reference.ToEnvelope())), CreateResolver(CreateStore(_ => Download())));
        await using var iterator = Read(client, chunkSize).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        Assert.True(await iterator.MoveNextAsync());
        if (missing)
        {
            var error = await Assert.ThrowsAsync<PayloadNotFoundException>(() => iterator.MoveNextAsync().AsTask());
            Assert.Equal(Reference.ToEnvelope(), error.Reference.ToEnvelope());
            Assert.Same(failure, error.InnerException);
        }
        else
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => iterator.MoveNextAsync().AsTask()));
        Assert.True(disposed);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task CancellationAbortsDownloadAndDisposes(int? chunkSize, bool cancelEnumeratorToken)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposed = false;
        async IAsyncEnumerable<ReadOnlyMemory<byte>> Download([EnumeratorCancellation] CancellationToken token)
        {
            try
            {
                yield return new byte[] { 1, 2 };
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            finally { disposed = true; }
        }
        var client = CreateClient(CreateGrpc(Response(Reference.ToEnvelope())), CreateResolver(CreateStore(Download)));
        using var methodCancellation = new CancellationTokenSource();
        using var enumeratorCancellation = new CancellationTokenSource();
        var chunks = chunkSize is { } size
            ? client.ReadWorkflowOutputChunksAsync("instance", size, methodCancellation.Token)
            : client.ReadWorkflowOutputChunksAsync("instance", methodCancellation.Token);
        await using var iterator = chunks.GetAsyncEnumerator(enumeratorCancellation.Token);
        try
        {
            Assert.True(await iterator.MoveNextAsync());
            var pending = iterator.MoveNextAsync().AsTask();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await (cancelEnumeratorToken ? enumeratorCancellation : methodCancellation).CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.True(disposed);
        }
        finally { await methodCancellation.CancelAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationStopsMetadataLookup(bool alreadyCanceled)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<GetInstanceResponse> GetResponse(CancellationToken token)
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("Metadata read should have been canceled.");
        }
        var grpc = CreateGrpc();
        grpc.Setup(g => g.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()))
            .Returns((GetInstanceRequest _, CallOptions options) => Unary(GetResponse(options.CancellationToken)));
        var client = CreateClient(grpc, new Mock<IWorkflowPayloadResolver>(MockBehavior.Strict).Object);
        using var cancellation = new CancellationTokenSource();
        if (alreadyCanceled)
            await cancellation.CancelAsync();
        await using var iterator = client.ReadWorkflowOutputChunksAsync("instance", TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(cancellation.Token);
        try
        {
            var pending = iterator.MoveNextAsync().AsTask();
            if (!alreadyCanceled)
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                await cancellation.CancelAsync();
            }
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            if (alreadyCanceled)
                grpc.VerifyNoOtherCalls();
        }
        finally { await cancellation.CancelAsync(); }
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(false, 3)]
    [InlineData(true, null)]
    [InlineData(true, 3)]
    public async Task BothRegistrationPathsUseRegisteredResolver(bool useBuilder, int? chunkSize)
    {
        async IAsyncEnumerable<ReadOnlyMemory<byte>> Download()
        {
            await Task.Yield();
            yield return new byte[] { 7 };
        }
        var resolver = new Mock<IWorkflowPayloadResolver>(MockBehavior.Strict);
        if (chunkSize is { } size)
            resolver.Setup(r => r.ReadChunksAsync(It.Is<PayloadReference>(p => p.Key == Reference.Key),
                size, It.IsAny<CancellationToken>())).Returns(Download);
        else
            resolver.Setup(r => r.ReadChunksAsync(It.Is<PayloadReference>(p => p.Key == Reference.Key),
                It.IsAny<CancellationToken>())).Returns(Download);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDaprWorkflow(_ => { });
        var grpc = CreateGrpc(Response(Reference.ToEnvelope()));
        services.AddSingleton(grpc.Object);
        services.AddSingleton(resolver.Object);
        await using var provider = services.BuildServiceProvider();
        Assert.Same(grpc.Object, provider.GetRequiredService<TaskHubSidecarService.TaskHubSidecarServiceClient>());
        await using var client = useBuilder
            ? new DaprWorkflowClientBuilder().UseServiceProvider(provider).Build()
            : new DaprWorkflowClient(provider.GetRequiredService<WorkflowClient>());
        var count = 0;
        await foreach (var chunk in Read(client, chunkSize))
        {
            Assert.Equal(new byte[] { 7 }, chunk.ToArray());
            count++;
        }
        Assert.Equal(1, count);
        resolver.VerifyAll();
    }

    private static IAsyncEnumerable<ReadOnlyMemory<byte>> Read(IDaprWorkflowClient client, int? chunkSize)
        => chunkSize is { } size
            ? client.ReadWorkflowOutputChunksAsync("instance", size, TestContext.Current.CancellationToken)
            : client.ReadWorkflowOutputChunksAsync("instance", TestContext.Current.CancellationToken);

    private static GetInstanceResponse Response(string? output) => new()
    {
        Exists = true,
        WorkflowState = new GrpcWorkflowState
        {
            InstanceId = "instance",
            WorkflowStatus = OrchestrationStatus.Completed,
            Output = output,
            Input = "{\"$daprRef\":{\"v\":999}}",
            CustomStatus = "{\"$daprRef\":"
        }
    };

    private static Mock<TaskHubSidecarService.TaskHubSidecarServiceClient> CreateGrpc(GetInstanceResponse? response = null)
    {
        var grpc = new Mock<TaskHubSidecarService.TaskHubSidecarServiceClient>(
            MockBehavior.Strict, new Mock<CallInvoker>().Object);
        if (response is not null)
        {
            grpc.Setup(g => g.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()))
                .Returns(() => Unary(Task.FromResult(response)));
        }
        return grpc;
    }

    private static AsyncUnaryCall<GetInstanceResponse> Unary(Task<GetInstanceResponse> response)
        => new(response, Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });

    private static DaprWorkflowClient CreateClient(
        Mock<TaskHubSidecarService.TaskHubSidecarServiceClient> grpc, IWorkflowPayloadResolver? resolver)
        => new(new WorkflowGrpcClient(grpc.Object, NullLogger<WorkflowGrpcClient>.Instance,
            new Mock<IDaprSerializer>(MockBehavior.Strict).Object, "token",
            new Mock<IWorkflowPayloadConverter>(MockBehavior.Strict).Object, resolver));

    private static Mock<DaprBinaryStoreClient> CreateStore(Func<CancellationToken, IAsyncEnumerable<ReadOnlyMemory<byte>>> download)
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        store.Setup(s => s.GetBinaryFileChunksAsync("store", "key", It.IsAny<CancellationToken>()))
            .Returns((string _, string _, CancellationToken token) => download(token));
        return store;
    }

    private static IWorkflowPayloadResolver CreateResolver(
        Mock<DaprBinaryStoreClient> store, PayloadWritePreference policy = PayloadWritePreference.Preserve)
        => new BinaryStoreWorkflowPayloadResolver(new BinaryStorePayloadConverter(store.Object,
            Options.Create(new BinaryStorePayloadOptions
            {
                StoreName = "store",
                WritePreference = policy,
                OffloadSites = PayloadKind.None,
                MaxInMemoryRehydrationSize = long.MaxValue
            }), NullWorkflowPayloadManifest.Instance), new JsonDaprSerializer());
}
