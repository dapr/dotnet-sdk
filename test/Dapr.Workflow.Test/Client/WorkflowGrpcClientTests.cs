// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//  ------------------------------------------------------------------------

using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Client;
using Dapr.Workflow.Serialization;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Dapr.Workflow.Test.Client;

public class WorkflowGrpcClientTests
{
    [Fact]
    public async Task ScheduleNewWorkflowAsync_ShouldUseProvidedInstanceId_WhenOptionsHasInstanceId()
    {
        var serializer = new StubSerializer { SerializeResult = "{\"x\":1}" };

        CreateInstanceRequest? capturedRequest = null;

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.StartInstanceAsync(It.IsAny<CreateInstanceRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<CreateInstanceRequest, Metadata?, DateTime?, CancellationToken>((r, _, _, _) => capturedRequest = r)
            .Returns(CreateAsyncUnaryCall(new CreateInstanceResponse { InstanceId = "id-from-sidecar" }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var instanceId = await client.ScheduleNewWorkflowAsync(
            "MyWorkflow",
            input: new { A = 1 },
            options: new StartWorkflowOptions { InstanceId = "instance-123" });

        Assert.Equal("id-from-sidecar", instanceId);
        Assert.NotNull(capturedRequest);
        Assert.Equal("instance-123", capturedRequest!.InstanceId);
        Assert.Equal("MyWorkflow", capturedRequest.Name);
        Assert.Equal("{\"x\":1}", capturedRequest.Input);
    }

    [Fact]
    public async Task ScheduleNewWorkflowAsync_ShouldGenerateNonEmptyInstanceId_WhenOptionsInstanceIdIsNull()
    {
        var serializer = new StubSerializer { SerializeResult = "" };

        CreateInstanceRequest? capturedRequest = null;

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.StartInstanceAsync(It.IsAny<CreateInstanceRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<CreateInstanceRequest, Metadata?, DateTime?, CancellationToken>((r, _, _, _) => capturedRequest = r)
            .Returns(CreateAsyncUnaryCall(new CreateInstanceResponse { InstanceId = "returned" }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await client.ScheduleNewWorkflowAsync("MyWorkflow", input: null, options: new StartWorkflowOptions { InstanceId = null });

        Assert.NotNull(capturedRequest);
        Assert.False(string.IsNullOrEmpty(capturedRequest!.InstanceId));
    }

    [Fact]
    public async Task ScheduleNewWorkflowAsync_ShouldSetScheduledStartTimestamp_WhenStartAtSpecified()
    {
        var serializer = new StubSerializer { SerializeResult = "" };
        var startAt = new DateTimeOffset(2025, 01, 02, 03, 04, 05, TimeSpan.Zero);

        CreateInstanceRequest? capturedRequest = null;

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.StartInstanceAsync(It.IsAny<CreateInstanceRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<CreateInstanceRequest, Metadata?, DateTime?, CancellationToken>((r, _, _, _) => capturedRequest = r)
            .Returns(CreateAsyncUnaryCall(new CreateInstanceResponse { InstanceId = "returned" }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await client.ScheduleNewWorkflowAsync(
            "MyWorkflow",
            input: null,
            options: new StartWorkflowOptions { InstanceId = "i", StartAt = startAt });

        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedRequest!.ScheduledStartTimestamp);
        Assert.Equal(startAt, capturedRequest.ScheduledStartTimestamp.ToDateTimeOffset());
    }

    [Fact]
    public async Task GetWorkflowMetadataAsync_ShouldReturnNull_WhenResponseExistsIsFalse()
    {
        var serializer = new StubSerializer();

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncUnaryCall(new GetInstanceResponse { Exists = false }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var result = await client.GetWorkflowMetadataAsync("missing", getInputsAndOutputs: true);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetWorkflowMetadataAsync_ShouldReturnNull_WhenGrpcThrowsNotFound()
    {
        var serializer = new StubSerializer();

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncUnaryCallThrows<GetInstanceResponse>(new RpcException(new Status(StatusCode.NotFound, "not found"))));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var result = await client.GetWorkflowMetadataAsync("missing", getInputsAndOutputs: true);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetWorkflowMetadataAsync_ShouldPassThroughGetInputsAndOutputsFlag()
    {
        var serializer = new JsonWorkflowSerializer();

        GetInstanceRequest? captured = null;

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<GetInstanceRequest, Metadata?, DateTime?, CancellationToken>((r, _, _, _) => captured = r)
            .Returns(CreateAsyncUnaryCall(new GetInstanceResponse
            {
                Exists = true,
                OrchestrationState = new OrchestrationState { InstanceId = "i", Name = "n", OrchestrationStatus = OrchestrationStatus.Running }
            }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await client.GetWorkflowMetadataAsync("i", getInputsAndOutputs: false);

        Assert.NotNull(captured);
        Assert.Equal("i", captured!.InstanceId);
        Assert.False(captured.GetInputsAndOutputs);
    }

    [Fact]
    public async Task WaitForWorkflowStartAsync_ShouldReturnImmediately_WhenStatusIsNotPending()
    {
        var serializer = new JsonWorkflowSerializer();

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncUnaryCall(new GetInstanceResponse
            {
                Exists = true,
                OrchestrationState = new OrchestrationState
                {
                    InstanceId = "i",
                    Name = "n",
                    OrchestrationStatus = OrchestrationStatus.Running
                }
            }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var result = await client.WaitForWorkflowStartAsync("i", getInputsAndOutputs: true);

        Assert.Equal("i", result.InstanceId);
        Assert.Equal(WorkflowRuntimeStatus.Running, result.RuntimeStatus);
    }

    [Fact]
    public async Task WaitForWorkflowStartAsync_ShouldThrowInvalidOperationException_WhenInstanceDoesNotExist()
    {
        var serializer = new JsonWorkflowSerializer();

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncUnaryCall(new GetInstanceResponse { Exists = false }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.WaitForWorkflowStartAsync("missing", getInputsAndOutputs: true));
    }

    [Fact]
    public async Task WaitForWorkflowCompletionAsync_ShouldReturnImmediately_WhenStatusIsTerminal()
    {
        var serializer = new JsonWorkflowSerializer();

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncUnaryCall(new GetInstanceResponse
            {
                Exists = true,
                OrchestrationState = new OrchestrationState
                {
                    InstanceId = "i",
                    Name = "n",
                    OrchestrationStatus = OrchestrationStatus.Completed,
                    Output = "{\"ok\":true}"
                }
            }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var result = await client.WaitForWorkflowCompletionAsync("i", getInputsAndOutputs: true);

        Assert.Equal("i", result.InstanceId);
        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal("{\"ok\":true}", result.SerializedOutput);
    }

    [Fact]
    public async Task RaiseEventAsync_ShouldThrowArgumentException_WhenInstanceIdIsNullOrEmpty()
    {
        var serializer = new StubSerializer();
        var grpcClientMock = CreateGrpcClientMock();
        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await Assert.ThrowsAsync<ArgumentException>(() => client.RaiseEventAsync("", "evt", eventPayload: null));
    }

    [Fact]
    public async Task RaiseEventAsync_ShouldThrowArgumentException_WhenEventNameIsNullOrEmpty()
    {
        var serializer = new StubSerializer();
        var grpcClientMock = CreateGrpcClientMock();
        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await Assert.ThrowsAsync<ArgumentException>(() => client.RaiseEventAsync("i", "", eventPayload: null));
    }

    [Fact]
    public async Task RaiseEventAsync_ShouldSendSerializedPayload()
    {
        var serializer = new StubSerializer { SerializeResult = "{\"p\":1}" };
        RaiseEventRequest? captured = null;

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.RaiseEventAsync(It.IsAny<RaiseEventRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<RaiseEventRequest, Metadata?, DateTime?, CancellationToken>((r, _, _, _) => captured = r)
            .Returns(CreateAsyncUnaryCall(new RaiseEventResponse()));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await client.RaiseEventAsync("i", "evt", new { P = 1 });

        Assert.NotNull(captured);
        Assert.Equal("i", captured!.InstanceId);
        Assert.Equal("evt", captured.Name);
        Assert.Equal("{\"p\":1}", captured.Input);
    }

    [Fact]
    public async Task TerminateWorkflowAsync_ShouldSendRecursiveTrue_AndSerializedOutput()
    {
        var serializer = new StubSerializer { SerializeResult = "{\"done\":true}" };
        TerminateRequest? captured = null;

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.TerminateInstanceAsync(It.IsAny<TerminateRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<TerminateRequest, Metadata?, DateTime?, CancellationToken>((r, _, _, _) => captured = r)
            .Returns(CreateAsyncUnaryCall(new TerminateResponse()));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await client.TerminateWorkflowAsync("i", output: new { Done = true });

        Assert.NotNull(captured);
        Assert.Equal("i", captured!.InstanceId);
        Assert.True(captured.Recursive);
        Assert.Equal("{\"done\":true}", captured.Output);
    }

    [Fact]
    public async Task SuspendWorkflowAsync_ShouldSendEmptyReason_WhenReasonIsNull()
    {
        var serializer = new StubSerializer();
        SuspendRequest? captured = null;

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.SuspendInstanceAsync(It.IsAny<SuspendRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<SuspendRequest, Metadata?, DateTime?, CancellationToken>((r, _, _, _) => captured = r)
            .Returns(CreateAsyncUnaryCall(new SuspendResponse()));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await client.SuspendWorkflowAsync("i", reason: null);

        Assert.NotNull(captured);
        Assert.Equal("i", captured!.InstanceId);
        Assert.Equal(string.Empty, captured.Reason);
    }

    [Fact]
    public async Task ResumeWorkflowAsync_ShouldSendEmptyReason_WhenReasonIsNull()
    {
        var serializer = new StubSerializer();
        ResumeRequest? captured = null;

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.ResumeInstanceAsync(It.IsAny<ResumeRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<ResumeRequest, Metadata?, DateTime?, CancellationToken>((r, _, _, _) => captured = r)
            .Returns(CreateAsyncUnaryCall(new ResumeResponse()));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await client.ResumeWorkflowAsync("i", reason: null);

        Assert.NotNull(captured);
        Assert.Equal("i", captured!.InstanceId);
        Assert.Equal(string.Empty, captured.Reason);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public async Task PurgeInstanceAsync_ShouldReturnTrueOnlyWhenDeletedInstanceCountGreaterThanZero(int deletedCount, bool expected)
    {
        var serializer = new StubSerializer();
        PurgeInstancesRequest? captured = null;

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.PurgeInstancesAsync(It.IsAny<PurgeInstancesRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Callback<PurgeInstancesRequest, Metadata?, DateTime?, CancellationToken>((r, _, _, _) => captured = r)
            .Returns(CreateAsyncUnaryCall(new PurgeInstancesResponse { DeletedInstanceCount = deletedCount }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var result = await client.PurgeInstanceAsync("i");

        Assert.NotNull(captured);
        Assert.Equal("i", captured!.InstanceId);
        Assert.True(captured.Recursive);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task DisposeAsync_ShouldCompleteSynchronously()
    {
        var serializer = new StubSerializer();
        var grpcClientMock = CreateGrpcClientMock();

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await client.DisposeAsync();
    }
    
    [Fact]
    public async Task WaitForWorkflowStartAsync_ShouldPollUntilStatusIsNotPending()
    {
        var serializer = new JsonWorkflowSerializer();

        var grpcClientMock = CreateGrpcClientMock();

        var requests = new List<GetInstanceRequest>();
        var callCount = 0;

        grpcClientMock
            .Setup(x => x.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns((GetInstanceRequest request, Metadata? _, DateTime? __, CancellationToken ___) =>
            {
                requests.Add(request);
                callCount++;

                var status = callCount == 1 ? OrchestrationStatus.Pending : OrchestrationStatus.Running;

                return CreateAsyncUnaryCall(new GetInstanceResponse
                {
                    Exists = true,
                    OrchestrationState = new OrchestrationState
                    {
                        InstanceId = "i",
                        Name = "n",
                        OrchestrationStatus = status
                    }
                });
            });

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var result = await client.WaitForWorkflowStartAsync("i", getInputsAndOutputs: false, cancellationToken: cts.Token);

        Assert.Equal("i", result.InstanceId);
        Assert.Equal(WorkflowRuntimeStatus.Running, result.RuntimeStatus);

        Assert.True(requests.Count >= 2);
        Assert.All(requests, r => Assert.Equal("i", r.InstanceId));
        Assert.All(requests, r => Assert.False(r.GetInputsAndOutputs));
    }

    [Fact]
    public async Task WaitForWorkflowCompletionAsync_ShouldPollUntilTerminalStatus()
    {
        var serializer = new JsonWorkflowSerializer();

        var grpcClientMock = CreateGrpcClientMock();

        var requests = new List<GetInstanceRequest>();
        var callCount = 0;

        grpcClientMock
            .Setup(x => x.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns((GetInstanceRequest request, Metadata? _, DateTime? __, CancellationToken ___) =>
            {
                requests.Add(request);
                callCount++;

                var status = callCount == 1 ? OrchestrationStatus.Running : OrchestrationStatus.Completed;

                return CreateAsyncUnaryCall(new GetInstanceResponse
                {
                    Exists = true,
                    OrchestrationState = new OrchestrationState
                    {
                        InstanceId = "i",
                        Name = "n",
                        OrchestrationStatus = status,
                        Output = status == OrchestrationStatus.Completed ? "\"done\"" : string.Empty
                    }
                });
            });

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var result = await client.WaitForWorkflowCompletionAsync("i", getInputsAndOutputs: true, cancellationToken: cts.Token);

        Assert.Equal("i", result.InstanceId);
        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal("\"done\"", result.SerializedOutput);

        Assert.True(requests.Count >= 2);
        Assert.All(requests, r => Assert.Equal("i", r.InstanceId));
        Assert.All(requests, r => Assert.True(r.GetInputsAndOutputs));
    }
    
    [Theory]
    [InlineData(OrchestrationStatus.Failed, WorkflowRuntimeStatus.Failed)]
    [InlineData(OrchestrationStatus.Terminated, WorkflowRuntimeStatus.Terminated)]
    public async Task WaitForWorkflowCompletionAsync_ShouldReturnImmediately_WhenStatusIsTerminalFailedOrTerminated(
        OrchestrationStatus protoStatus,
        WorkflowRuntimeStatus expectedStatus)
    {
        var serializer = new JsonWorkflowSerializer();

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncUnaryCall(new GetInstanceResponse
            {
                Exists = true,
                OrchestrationState = new OrchestrationState
                {
                    InstanceId = "i",
                    Name = "n",
                    OrchestrationStatus = protoStatus
                }
            }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var result = await client.WaitForWorkflowCompletionAsync("i", getInputsAndOutputs: true);

        Assert.Equal("i", result.InstanceId);
        Assert.Equal(expectedStatus, result.RuntimeStatus);
    }

    [Fact]
    public async Task WaitForWorkflowCompletionAsync_ShouldThrowInvalidOperationException_WhenInstanceDoesNotExist()
    {
        var serializer = new JsonWorkflowSerializer();

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncUnaryCall(new GetInstanceResponse { Exists = false }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.WaitForWorkflowCompletionAsync("missing", getInputsAndOutputs: true));

        Assert.Contains("missing", ex.Message);
    }

    [Fact]
    public async Task WaitForWorkflowCompletionAsync_ShouldRespectCancellationToken_WhileWaiting()
    {
        var serializer = new JsonWorkflowSerializer();

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncUnaryCall(new GetInstanceResponse
            {
                Exists = true,
                OrchestrationState = new OrchestrationState
                {
                    InstanceId = "i",
                    Name = "n",
                    OrchestrationStatus = OrchestrationStatus.Running
                }
            }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.WaitForWorkflowCompletionAsync("i", getInputsAndOutputs: true, cancellationToken: cts.Token));
    }

    private static Mock<TaskHubSidecarService.TaskHubSidecarServiceClient> CreateGrpcClientMock()
    {
        var callInvoker = new Mock<CallInvoker>(MockBehavior.Loose);
        return new Mock<TaskHubSidecarService.TaskHubSidecarServiceClient>(MockBehavior.Loose, callInvoker.Object);
    }

    private static AsyncUnaryCall<T> CreateAsyncUnaryCall<T>(T response)
        where T : class
    {
        return new AsyncUnaryCall<T>(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }

    private static AsyncUnaryCall<T> CreateAsyncUnaryCallThrows<T>(Exception ex)
        where T : class
    {
        return new AsyncUnaryCall<T>(
            Task.FromException<T>(ex),
            Task.FromResult(new Metadata()),
            () => new Status(StatusCode.Unknown, "error"),
            () => new Metadata(),
            () => { });
    }

    private sealed class StubSerializer : IWorkflowSerializer
    {
        public string SerializeResult { get; set; } = string.Empty;

        public string Serialize(object? value, Type? inputType = null) => value is null ? string.Empty : SerializeResult;

        public T? Deserialize<T>(string? data) => throw new NotSupportedException();

        public object? Deserialize(string? data, Type returnType) => throw new NotSupportedException();
    }
}
