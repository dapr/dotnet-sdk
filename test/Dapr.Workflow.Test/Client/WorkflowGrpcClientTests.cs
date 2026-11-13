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

using Dapr.DurableTask.Protobuf;
using Dapr.Testcontainers.Xunit.Attributes;
using Dapr.Workflow.Client;
using Dapr.Common.Serialization;
using Dapr.Workflow.Serialization;
using Dapr.Workflow.Payloads;
using DelayedPayloadConverter = Dapr.Workflow.Test.Payloads.BinaryStorePayloadTests.DelayedPayloadConverter;
using Grpc.Core;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Dapr.Workflow.Test.Client;

public class WorkflowGrpcClientTests
{
    [MinimumDaprRuntimeFact("1.19")]
    public async Task PayloadCodecTransformsClientWritesAfterConfiguredSerialization()
    {
        var codec = new DelayedPayloadConverter();
        var serializer = new StubSerializer { SerializeResult = "custom-serialized" };
        var grpc = CreateGrpcClientMock();
        CreateInstanceRequest? scheduled = null;
        RaiseEventRequest? raised = null;
        TerminateRequest? terminated = null;
        RerunWorkflowFromEventRequest? rerun = null;
        grpc.Setup(c => c.StartInstanceAsync(It.IsAny<CreateInstanceRequest>(), It.IsAny<CallOptions>()))
            .Callback<CreateInstanceRequest, CallOptions>((request, _) => scheduled = request)
            .Returns(CreateAsyncUnaryCall(new CreateInstanceResponse { InstanceId = "instance" }));
        grpc.Setup(c => c.RaiseEventAsync(It.IsAny<RaiseEventRequest>(), It.IsAny<CallOptions>()))
            .Callback<RaiseEventRequest, CallOptions>((request, _) => raised = request)
            .Returns(CreateAsyncUnaryCall(new RaiseEventResponse()));
        grpc.Setup(c => c.TerminateInstanceAsync(It.IsAny<TerminateRequest>(), It.IsAny<CallOptions>()))
            .Callback<TerminateRequest, CallOptions>((request, _) => terminated = request)
            .Returns(CreateAsyncUnaryCall(new TerminateResponse()));
        grpc.Setup(c => c.RerunWorkflowFromEventAsync(It.IsAny<RerunWorkflowFromEventRequest>(), It.IsAny<CallOptions>()))
            .Callback<RerunWorkflowFromEventRequest, CallOptions>((request, _) => rerun = request)
            .Returns(CreateAsyncUnaryCall(new RerunWorkflowFromEventResponse { NewInstanceID = "rerun" }));
        var client = new WorkflowGrpcClient(grpc.Object, NullLogger<WorkflowGrpcClient>.Instance,
            serializer, payloadConverter: codec);
        var token = TestContext.Current.CancellationToken;

        await client.ScheduleNewWorkflowAsync("workflow", 42, new StartWorkflowOptions { InstanceId = "instance" }, token);
        await client.RaiseEventAsync("instance", "event", 42, token);
        await client.TerminateWorkflowAsync("instance", 42, token);
        await client.RerunWorkflowFromEventAsync("instance", 1, new() { OverwriteInput = true, Input = 42 }, token);

        Assert.Equal("wire:custom-serialized", scheduled!.Input);
        Assert.Equal("wire:custom-serialized", raised!.Input);
        Assert.Equal("wire:custom-serialized", terminated!.Output);
        Assert.Equal("wire:custom-serialized", rerun!.Input);
        Assert.False(string.IsNullOrWhiteSpace(rerun.NewInstanceID));
        Assert.Equal([PayloadKind.WorkflowInput, PayloadKind.ExternalEvent, PayloadKind.WorkflowOutput, PayloadKind.WorkflowInput],
            codec.Writes.Select(write => write.Site.Kind));
        Assert.Equal(rerun.NewInstanceID, codec.Writes[^1].Site.InstanceId);
    }

    [MinimumDaprRuntimeTheory("1.19")]
    [InlineData("get")]
    [InlineData("start")]
    [InlineData("complete")]
    public async Task MetadataPayloadsArePreparedBeforeSynchronousReads(string operation)
    {
        var codec = new DelayedPayloadConverter();
        var grpc = CreateGrpcClientMock();
        var response = new GetInstanceResponse
        {
            Exists = true,
            WorkflowState = new()
            {
                InstanceId = "instance", Name = "workflow",
                Input = "wire:1", Output = "wire:2", CustomStatus = "wire:3"
            }
        };
        grpc.Setup(c => c.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateAsyncUnaryCall(response));
        grpc.Setup(c => c.WaitForInstanceStartAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateAsyncUnaryCall(response));
        grpc.Setup(c => c.WaitForInstanceCompletionAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateAsyncUnaryCall(response));
        var client = new WorkflowGrpcClient(grpc.Object, NullLogger<WorkflowGrpcClient>.Instance,
            new JsonDaprSerializer(), payloadConverter: codec);
        var token = TestContext.Current.CancellationToken;

        var metadata = operation switch
        {
            "get" => await client.GetWorkflowMetadataAsync("instance", true, token),
            "start" => await client.WaitForWorkflowStartAsync("instance", true, token),
            _ => await client.WaitForWorkflowCompletionAsync("instance", true, token)
        };

        Assert.NotNull(metadata);
        Assert.Equal(1, metadata.ReadInputAs<int>());
        Assert.Equal(2, metadata.ReadOutputAs<int>());
        Assert.Equal(3, metadata.ReadCustomStatusAs<int>());
        Assert.Equal(3, codec.Reads.Count);
    }

    [MinimumDaprRuntimeFact("1.19")]
    public async Task MissingPayloadMustNotBeReportedAsMissingWorkflowMetadata()
    {
        var codec = new Mock<IWorkflowPayloadConverter>();
        codec.Setup(c => c.DecodeAsync(It.IsAny<string>(), It.IsAny<PayloadSite>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RpcException(new Status(StatusCode.NotFound, "missing payload")));
        var grpc = CreateGrpcClientMock();
        grpc.Setup(c => c.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateAsyncUnaryCall(new GetInstanceResponse { Exists = true, WorkflowState = new() { Input = "wire" } }));
        var client = new WorkflowGrpcClient(grpc.Object, NullLogger<WorkflowGrpcClient>.Instance,
            new JsonDaprSerializer(), payloadConverter: codec.Object);

        await Assert.ThrowsAsync<RpcException>(() =>
            client.GetWorkflowMetadataAsync("instance", true, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ScheduleNewWorkflowAsync_ShouldUseProvidedInstanceId_WhenOptionsHasInstanceId()
    {
        var serializer = new StubSerializer { SerializeResult = "{\"x\":1}" };

        CreateInstanceRequest? capturedRequest = null;
        CallOptions? capturedCallOptions = null;

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.StartInstanceAsync(It.IsAny<CreateInstanceRequest>(), It.IsAny<CallOptions>()))
            .Callback<CreateInstanceRequest, CallOptions>((r, options) =>
            {
                capturedRequest = r;
                capturedCallOptions = options;
            })
            .Returns(CreateAsyncUnaryCall(new CreateInstanceResponse { InstanceId = "id-from-sidecar" }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var instanceId = await client.ScheduleNewWorkflowAsync("MyWorkflow", input: new { A = 1 }, options: new StartWorkflowOptions { InstanceId = "instance-123" }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("id-from-sidecar", instanceId);
        Assert.NotNull(capturedRequest);
        Assert.Equal("instance-123", capturedRequest!.InstanceId);
        Assert.Equal("MyWorkflow", capturedRequest.Name);
        Assert.Equal("{\"x\":1}", capturedRequest.Input);
        Assert.True(capturedCallOptions?.IsWaitForReady);
    }

    [Fact]
    public async Task ScheduleNewWorkflowAsync_ShouldGenerateNonEmptyInstanceId_WhenOptionsInstanceIdIsNull()
    {
        var serializer = new StubSerializer { SerializeResult = "" };

        CreateInstanceRequest? capturedRequest = null;

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.StartInstanceAsync(It.IsAny<CreateInstanceRequest>(), It.IsAny<CallOptions>()))
            .Callback<CreateInstanceRequest, CallOptions>((r, _) => capturedRequest = r)
            .Returns(CreateAsyncUnaryCall(new CreateInstanceResponse { InstanceId = "returned" }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await client.ScheduleNewWorkflowAsync("MyWorkflow", input: null, options: new StartWorkflowOptions { InstanceId = null }, cancellationToken: TestContext.Current.CancellationToken);

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
            .Setup(x => x.StartInstanceAsync(It.IsAny<CreateInstanceRequest>(), It.IsAny<CallOptions>()))
            .Callback<CreateInstanceRequest, CallOptions>((r, _) => capturedRequest = r)
            .Returns(CreateAsyncUnaryCall(new CreateInstanceResponse { InstanceId = "returned" }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await client.ScheduleNewWorkflowAsync("MyWorkflow", input: null, options: new StartWorkflowOptions { InstanceId = "i", StartAt = startAt }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedRequest!.ScheduledStartTimestamp);
        Assert.Equal(startAt, capturedRequest.ScheduledStartTimestamp.ToDateTimeOffset());
    }

    [Fact]
    public async Task ScheduleNewWorkflowAsync_ShouldSetParentTraceContext_WhenActivityCurrentIsW3C()
    {
        var serializer = new StubSerializer { SerializeResult = "" };

        CreateInstanceRequest? capturedRequest = null;

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.StartInstanceAsync(It.IsAny<CreateInstanceRequest>(), It.IsAny<CallOptions>()))
            .Callback<CreateInstanceRequest, CallOptions>((r, _) => capturedRequest = r)
            .Returns(CreateAsyncUnaryCall(new CreateInstanceResponse { InstanceId = "returned" }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var previous = Activity.Current;
        using var activity = new Activity("parent");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.TraceStateString = "vendor=value";
        activity.Start();

        try
        {
            await client.ScheduleNewWorkflowAsync("MyWorkflow", input: null, options: new StartWorkflowOptions { InstanceId = "i" }, cancellationToken: TestContext.Current.CancellationToken);
        }
        finally
        {
            Activity.Current = previous;
        }

        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedRequest!.ParentTraceContext);
        Assert.Equal(activity.Id, capturedRequest.ParentTraceContext.TraceParent);
        Assert.Equal(activity.TraceStateString, capturedRequest.ParentTraceContext.TraceState);
    }

    [Fact]
    public async Task ScheduleNewWorkflowAsync_ShouldNotSetParentTraceContext_WhenActivityCurrentIsNull()
    {
        var serializer = new StubSerializer { SerializeResult = "" };

        CreateInstanceRequest? capturedRequest = null;

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.StartInstanceAsync(It.IsAny<CreateInstanceRequest>(), It.IsAny<CallOptions>()))
            .Callback<CreateInstanceRequest, CallOptions>((r, _) => capturedRequest = r)
            .Returns(CreateAsyncUnaryCall(new CreateInstanceResponse { InstanceId = "returned" }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var previous = Activity.Current;
        Activity.Current = null;

        try
        {
            await client.ScheduleNewWorkflowAsync("MyWorkflow", input: null, options: new StartWorkflowOptions { InstanceId = "i" }, cancellationToken: TestContext.Current.CancellationToken);
        }
        finally
        {
            Activity.Current = previous;
        }

        Assert.NotNull(capturedRequest);
        Assert.Null(capturedRequest!.ParentTraceContext);
    }

    [Fact]
    public async Task GetWorkflowMetadataAsync_ShouldReturnNull_WhenResponseExistsIsFalse()
    {
        var serializer = new StubSerializer();

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateAsyncUnaryCall(new GetInstanceResponse { Exists = false }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var result = await client.GetWorkflowMetadataAsync("missing", getInputsAndOutputs: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetWorkflowMetadataAsync_ShouldReturnNull_WhenGrpcThrowsNotFound()
    {
        var serializer = new StubSerializer();

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateAsyncUnaryCallThrows<GetInstanceResponse>(new RpcException(new Status(StatusCode.NotFound, "not found"))));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var result = await client.GetWorkflowMetadataAsync("missing", getInputsAndOutputs: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetWorkflowMetadataAsync_ShouldPassThroughGetInputsAndOutputsFlag()
    {
        var serializer = new JsonDaprSerializer();

        GetInstanceRequest? captured = null;

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()))
            .Callback<GetInstanceRequest, CallOptions>((r, _) => captured = r)
            .Returns(CreateAsyncUnaryCall(new GetInstanceResponse
            {
                Exists = true,
                WorkflowState = new Dapr.DurableTask.Protobuf.WorkflowState { InstanceId = "i", Name = "n", WorkflowStatus = OrchestrationStatus.Running }
            }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await client.GetWorkflowMetadataAsync("i", getInputsAndOutputs: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal("i", captured!.InstanceId);
        Assert.False(captured.GetInputsAndOutputs);
    }

    [Fact]
    public async Task WaitForWorkflowStartAsync_ShouldUseServerBlockingRpc_AndReturnStartedState()
    {
        var serializer = new JsonDaprSerializer();

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.WaitForInstanceStartAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateAsyncUnaryCall(new GetInstanceResponse
            {
                Exists = true,
                WorkflowState = new Dapr.DurableTask.Protobuf.WorkflowState
                {
                    InstanceId = "i",
                    Name = "n",
                    WorkflowStatus = OrchestrationStatus.Running
                }
            }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var result = await client.WaitForWorkflowStartAsync("i", getInputsAndOutputs: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("i", result.InstanceId);
        Assert.Equal(WorkflowRuntimeStatus.Running, result.RuntimeStatus);
        grpcClientMock.Verify(x => x.WaitForInstanceStartAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()), Times.Once);
        grpcClientMock.Verify(x => x.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()), Times.Never);
    }

    [Fact]
    public async Task WaitForWorkflowStartAsync_ShouldThrowInvalidOperationException_WhenInstanceDoesNotExist()
    {
        var serializer = new JsonDaprSerializer();

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.WaitForInstanceStartAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateAsyncUnaryCall(new GetInstanceResponse { Exists = false }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.WaitForWorkflowStartAsync("missing", getInputsAndOutputs: true, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WaitForWorkflowCompletionAsync_ShouldUseServerBlockingRpc_AndReturnTerminalState()
    {
        var serializer = new JsonDaprSerializer();

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.WaitForInstanceCompletionAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateAsyncUnaryCall(new GetInstanceResponse
            {
                Exists = true,
                WorkflowState = new Dapr.DurableTask.Protobuf.WorkflowState
                {
                    InstanceId = "i",
                    Name = "n",
                    WorkflowStatus = OrchestrationStatus.Completed,
                    Output = "{\"ok\":true}"
                }
            }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var result = await client.WaitForWorkflowCompletionAsync("i", getInputsAndOutputs: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("i", result.InstanceId);
        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal("{\"ok\":true}", result.SerializedOutput);
        grpcClientMock.Verify(x => x.WaitForInstanceCompletionAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()), Times.Once);
        grpcClientMock.Verify(x => x.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()), Times.Never);
    }

    [Fact]
    public async Task WaitForWorkflowCompletionAsync_ShouldRetry_WhenWaitIsInterruptedByTransientError()
    {
        var serializer = new JsonDaprSerializer();

        var attempts = 0;
        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.WaitForInstanceCompletionAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()))
            .Returns(() =>
            {
                attempts++;
                if (attempts == 1)
                {
                    return CreateAsyncUnaryCallThrows<GetInstanceResponse>(
                        new RpcException(new Status(StatusCode.DeadlineExceeded, "deadline")));
                }

                return CreateAsyncUnaryCall(new GetInstanceResponse
                {
                    Exists = true,
                    WorkflowState = new Dapr.DurableTask.Protobuf.WorkflowState
                    {
                        InstanceId = "i",
                        Name = "n",
                        WorkflowStatus = OrchestrationStatus.Completed
                    }
                });
            });

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var result = await client.WaitForWorkflowCompletionAsync("i", getInputsAndOutputs: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task WaitForWorkflowCompletionAsync_ShouldNotRetry_WhenWaitFailsWithNonTransientError()
    {
        var serializer = new JsonDaprSerializer();

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.WaitForInstanceCompletionAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateAsyncUnaryCallThrows<GetInstanceResponse>(
                new RpcException(new Status(StatusCode.InvalidArgument, "bad"))));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var ex = await Assert.ThrowsAsync<RpcException>(() => client.WaitForWorkflowCompletionAsync("i", getInputsAndOutputs: true, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
        grpcClientMock.Verify(x => x.WaitForInstanceCompletionAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()), Times.Once);
    }

    [Fact]
    public async Task WaitForWorkflowCompletionAsync_ShouldThrowOperationCanceled_WhenCancelled()
    {
        var serializer = new JsonDaprSerializer();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var grpcClientMock = CreateGrpcClientMock();

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await Assert.ThrowsAsync<OperationCanceledException>(() => client.WaitForWorkflowCompletionAsync("i", getInputsAndOutputs: true, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task RaiseEventAsync_ShouldThrowArgumentException_WhenInstanceIdIsNullOrEmpty()
    {
        var serializer = new StubSerializer();
        var grpcClientMock = CreateGrpcClientMock();
        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await Assert.ThrowsAsync<ArgumentException>(() => client.RaiseEventAsync("", "evt", eventPayload: null, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RaiseEventAsync_ShouldThrowArgumentException_WhenEventNameIsNullOrEmpty()
    {
        var serializer = new StubSerializer();
        var grpcClientMock = CreateGrpcClientMock();
        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await Assert.ThrowsAsync<ArgumentException>(() => client.RaiseEventAsync("i", "", eventPayload: null, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RaiseEventAsync_ShouldSendSerializedPayload()
    {
        var serializer = new StubSerializer { SerializeResult = "{\"p\":1}" };
        RaiseEventRequest? captured = null;

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.RaiseEventAsync(It.IsAny<RaiseEventRequest>(), It.IsAny<CallOptions>()))
            .Callback<RaiseEventRequest, CallOptions>((r, _) => captured = r)
            .Returns(CreateAsyncUnaryCall(new RaiseEventResponse()));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await client.RaiseEventAsync("i", "evt", new { P = 1 }, TestContext.Current.CancellationToken);

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
            .Setup(x => x.TerminateInstanceAsync(It.IsAny<TerminateRequest>(), It.IsAny<CallOptions>()))
            .Callback<TerminateRequest, CallOptions>((r, _) => captured = r)
            .Returns(CreateAsyncUnaryCall(new TerminateResponse()));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await client.TerminateWorkflowAsync("i", output: new { Done = true }, cancellationToken: TestContext.Current.CancellationToken);

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
            .Setup(x => x.SuspendInstanceAsync(It.IsAny<SuspendRequest>(), It.IsAny<CallOptions>()))
            .Callback<SuspendRequest, CallOptions>((r, _) => captured = r)
            .Returns(CreateAsyncUnaryCall(new SuspendResponse()));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await client.SuspendWorkflowAsync("i", reason: null, cancellationToken: TestContext.Current.CancellationToken);

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
            .Setup(x => x.ResumeInstanceAsync(It.IsAny<ResumeRequest>(), It.IsAny<CallOptions>()))
            .Callback<ResumeRequest, CallOptions>((r, _) => captured = r)
            .Returns(CreateAsyncUnaryCall(new ResumeResponse()));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await client.ResumeWorkflowAsync("i", reason: null, cancellationToken: TestContext.Current.CancellationToken);

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
            .Setup(x => x.PurgeInstancesAsync(It.IsAny<PurgeInstancesRequest>(), It.IsAny<CallOptions>()))
            .Callback<PurgeInstancesRequest, CallOptions>((r, _) => captured = r)
            .Returns(CreateAsyncUnaryCall(new PurgeInstancesResponse { DeletedInstanceCount = deletedCount }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var result = await client.PurgeInstanceAsync("i", TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal("i", captured!.InstanceId);
        Assert.True(captured.Recursive);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ListInstanceIdsAsync_ShouldSendRequestAndReturnPage()
    {
        var serializer = new StubSerializer();
        ListInstanceIDsRequest? captured = null;

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.ListInstanceIDsAsync(It.IsAny<ListInstanceIDsRequest>(), It.IsAny<CallOptions>()))
            .Callback<ListInstanceIDsRequest, CallOptions>((r, _) => captured = r)
            .Returns(CreateAsyncUnaryCall(new ListInstanceIDsResponse
            {
                InstanceIds = { "id1", "id2", "id3" },
                ContinuationToken = "next-page"
            }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var result = await client.ListInstanceIdsAsync(continuationToken: "prev-token", pageSize: 3, instanceIdPrefix: "sched_",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal("prev-token", captured!.ContinuationToken);
        Assert.Equal(3u, captured.PageSize);
        Assert.Equal("sched_", captured.InstanceIdPrefix);
        Assert.Equal(3, result.InstanceIds.Count);
        Assert.Equal("id1", result.InstanceIds[0]);
        Assert.Equal("next-page", result.ContinuationToken);
    }

    [Fact]
    public async Task ListInstanceIdsAsync_ShouldReturnNullContinuationToken_WhenNoneInResponse()
    {
        var serializer = new StubSerializer();

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.ListInstanceIDsAsync(It.IsAny<ListInstanceIDsRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateAsyncUnaryCall(new ListInstanceIDsResponse
            {
                InstanceIds = { "id1" }
            }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var result = await client.ListInstanceIdsAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.InstanceIds);
        Assert.Null(result.ContinuationToken);
    }

    [Fact]
    public async Task CreateWorkflowScheduleAsync_ShouldSendAllOptions()
    {
        var serializer = new StubSerializer { SerializeResult = "{\"a\":1}" };
        CreateWorkflowScheduleRequest? captured = null;
        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.CreateWorkflowScheduleAsync(It.IsAny<CreateWorkflowScheduleRequest>(), It.IsAny<CallOptions>()))
            .Callback<CreateWorkflowScheduleRequest, CallOptions>((r, _) => captured = r)
            .Returns(CreateAsyncUnaryCall(new CreateWorkflowScheduleResponse()));
        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);
        var dueTime = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var expirationTime = dueTime.AddDays(7);

        await client.CreateWorkflowScheduleAsync("sched", "wf", "0 30 9 * * MON", new WorkflowScheduleOptions
        {
            Input = new { a = 1 },
            DueTime = dueTime,
            ExpirationTime = expirationTime,
            Repeats = 3,
            TargetAppId = "other",
            TargetAppNamespace = "ns",
            SkipMissed = true,
            Overwrite = true
        }, TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.True(captured!.Overwrite);
        var schedule = captured.Schedule;
        Assert.Equal("sched", schedule.Name);
        Assert.Equal("wf", schedule.WorkflowName);
        Assert.Equal("0 30 9 * * MON", schedule.Schedule);
        Assert.Equal("{\"a\":1}", schedule.Input);
        Assert.Equal(dueTime, schedule.DueTime.ToDateTimeOffset());
        Assert.Equal(expirationTime, schedule.ExpirationTime.ToDateTimeOffset());
        Assert.Equal(3u, schedule.Repeats);
        Assert.Equal("other", schedule.TargetAppID);
        Assert.Equal("ns", schedule.TargetAppNamespace);
        Assert.True(schedule.HasSkipMissed);
        Assert.True(schedule.SkipMissed);
        Assert.Null(schedule.Status);
    }

    [Fact]
    public async Task CreateWorkflowScheduleAsync_ShouldLeaveOptionalFieldsUnset_WhenNoOptions()
    {
        CreateWorkflowScheduleRequest? captured = null;
        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.CreateWorkflowScheduleAsync(It.IsAny<CreateWorkflowScheduleRequest>(), It.IsAny<CallOptions>()))
            .Callback<CreateWorkflowScheduleRequest, CallOptions>((r, _) => captured = r)
            .Returns(CreateAsyncUnaryCall(new CreateWorkflowScheduleResponse()));
        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, new StubSerializer());

        await client.CreateWorkflowScheduleAsync("sched", "wf", "@every 1m", cancellationToken: TestContext.Current.CancellationToken);

        var schedule = captured!.Schedule;
        Assert.False(captured.Overwrite);
        Assert.Null(schedule.Input);
        Assert.Null(schedule.DueTime);
        Assert.Null(schedule.ExpirationTime);
        Assert.False(schedule.HasRepeats);
        Assert.False(schedule.HasTargetAppID);
        Assert.False(schedule.HasTargetAppNamespace);
        Assert.False(schedule.HasSkipMissed);
    }

    [Fact]
    public async Task CreateWorkflowScheduleAsync_ShouldEncodeInputInline_WhenPayloadConverterIsConfigured()
    {
        var codec = new DelayedPayloadConverter();
        CreateWorkflowScheduleRequest? captured = null;
        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.CreateWorkflowScheduleAsync(It.IsAny<CreateWorkflowScheduleRequest>(), It.IsAny<CallOptions>()))
            .Callback<CreateWorkflowScheduleRequest, CallOptions>((r, _) => captured = r)
            .Returns(CreateAsyncUnaryCall(new CreateWorkflowScheduleResponse()));
        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance,
            new StubSerializer { SerializeResult = "42" }, payloadConverter: codec);

        await client.CreateWorkflowScheduleAsync("sched", "wf", "@daily", new WorkflowScheduleOptions { Input = 42 },
            TestContext.Current.CancellationToken);

        Assert.Equal("wire:42", captured!.Schedule.Input);
        var site = Assert.Single(codec.Writes).Site;
        Assert.Equal(PayloadKind.WorkflowInput, site.Kind);
        Assert.True(site.RequireInline);
    }

    [Fact]
    public async Task GetWorkflowScheduleAsync_ShouldConvertScheduleAndStatus()
    {
        var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        GetWorkflowScheduleRequest? captured = null;
        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.GetWorkflowScheduleAsync(It.IsAny<GetWorkflowScheduleRequest>(), It.IsAny<CallOptions>()))
            .Callback<GetWorkflowScheduleRequest, CallOptions>((r, _) => captured = r)
            .Returns(CreateAsyncUnaryCall(new GetWorkflowScheduleResponse
            {
                Schedule = new Dapr.DurableTask.Protobuf.WorkflowSchedule
                {
                    Name = "sched",
                    WorkflowName = "wf",
                    Schedule = "@hourly",
                    Input = "\"in\"",
                    Repeats = 5,
                    TargetAppID = "other",
                    SkipMissed = true,
                    Status = new Dapr.DurableTask.Protobuf.WorkflowScheduleStatus
                    {
                        State = Dapr.DurableTask.Protobuf.WorkflowScheduleState.Failed,
                        CreatedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(created),
                        LastRunTime = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(created.AddHours(1)),
                        RunCount = 2,
                        FailureMessage = "boom",
                        LastRun = new Dapr.DurableTask.Protobuf.WorkflowScheduleRun
                        {
                            InstanceId = "sched_20260101T010000Z",
                            RuntimeStatus = OrchestrationStatus.Failed,
                            Output = "\"out\"",
                            FailureDetails = new TaskFailureDetails { ErrorType = "E", ErrorMessage = "m" }
                        }
                    }
                }
            }));
        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, new StubSerializer());

        var result = await client.GetWorkflowScheduleAsync("sched", TestContext.Current.CancellationToken);

        Assert.Equal("sched", captured!.Name);
        Assert.NotNull(result);
        Assert.Equal(("sched", "wf", "@hourly"), (result!.Name, result.WorkflowName, result.Schedule));
        Assert.Equal("\"in\"", result.SerializedInput);
        Assert.Equal(5, result.Repeats);
        Assert.Equal("other", result.TargetAppId);
        Assert.Null(result.TargetAppNamespace);
        Assert.True(result.SkipMissed);
        Assert.Null(result.DueTime);
        var status = result.Status!;
        Assert.Equal(Dapr.Workflow.Client.WorkflowScheduleState.Failed, status.State);
        Assert.Equal(new DateTimeOffset(created), status.CreatedAt);
        Assert.Equal(new DateTimeOffset(created.AddHours(1)), status.LastRunTime);
        Assert.Null(status.NextRunTime);
        Assert.Equal(2, status.RunCount);
        Assert.Equal("boom", status.FailureMessage);
        var run = status.LastRun!;
        Assert.Equal("sched_20260101T010000Z", run.InstanceId);
        Assert.Equal(WorkflowRuntimeStatus.Failed, run.RuntimeStatus);
        Assert.Equal("\"out\"", run.SerializedOutput);
        Assert.Equal("m", run.FailureDetails!.ErrorMessage);
        Assert.Null(run.CreatedAt);
    }

    [Fact]
    public async Task GetWorkflowScheduleAsync_ShouldReturnNull_WhenNotFound()
    {
        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.GetWorkflowScheduleAsync(It.IsAny<GetWorkflowScheduleRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateAsyncUnaryCallThrows<GetWorkflowScheduleResponse>(
                new RpcException(new Status(StatusCode.NotFound, "not found"))));
        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, new StubSerializer());

        Assert.Null(await client.GetWorkflowScheduleAsync("missing", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetWorkflowScheduleAsync_ShouldDecodePayloads_WhenPayloadConverterIsConfigured()
    {
        var codec = new DelayedPayloadConverter();
        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.GetWorkflowScheduleAsync(It.IsAny<GetWorkflowScheduleRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateAsyncUnaryCall(new GetWorkflowScheduleResponse
            {
                Schedule = new Dapr.DurableTask.Protobuf.WorkflowSchedule
                {
                    Name = "sched",
                    WorkflowName = "wf",
                    Schedule = "@daily",
                    Input = "wire:1",
                    Status = new Dapr.DurableTask.Protobuf.WorkflowScheduleStatus
                    {
                        LastRun = new Dapr.DurableTask.Protobuf.WorkflowScheduleRun { InstanceId = "sched_x", Output = "wire:2" }
                    }
                }
            }));
        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance,
            new StubSerializer(), payloadConverter: codec);

        var result = await client.GetWorkflowScheduleAsync("sched", TestContext.Current.CancellationToken);

        Assert.Equal("1", result!.SerializedInput);
        Assert.Equal("2", result.Status!.LastRun!.SerializedOutput);
        Assert.Equal([PayloadKind.WorkflowInput, PayloadKind.WorkflowOutput], codec.Reads.Select(site => site.Kind));
    }

    [Fact]
    public async Task GetWorkflowScheduleAsync_ShouldPropagateOtherRpcErrors()
    {
        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.GetWorkflowScheduleAsync(It.IsAny<GetWorkflowScheduleRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateAsyncUnaryCallThrows<GetWorkflowScheduleResponse>(
                new RpcException(new Status(StatusCode.Unimplemented, "old runtime"))));
        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, new StubSerializer());

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            client.GetWorkflowScheduleAsync("sched", TestContext.Current.CancellationToken));
        Assert.Equal(StatusCode.Unimplemented, ex.StatusCode);
    }

    [Fact]
    public async Task ListWorkflowSchedulesAsync_ShouldSendRequestAndReturnPage()
    {
        ListWorkflowSchedulesRequest? captured = null;
        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.ListWorkflowSchedulesAsync(It.IsAny<ListWorkflowSchedulesRequest>(), It.IsAny<CallOptions>()))
            .Callback<ListWorkflowSchedulesRequest, CallOptions>((r, _) => captured = r)
            .Returns(CreateAsyncUnaryCall(new ListWorkflowSchedulesResponse
            {
                Schedules =
                {
                    new Dapr.DurableTask.Protobuf.WorkflowSchedule { Name = "a", WorkflowName = "wf", Schedule = "@daily" },
                    new Dapr.DurableTask.Protobuf.WorkflowSchedule { Name = "b", WorkflowName = "wf", Schedule = "@hourly" }
                },
                ContinuationToken = "next"
            }));
        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, new StubSerializer());

        var page = await client.ListWorkflowSchedulesAsync("prev", 2, TestContext.Current.CancellationToken);

        Assert.Equal("prev", captured!.ContinuationToken);
        Assert.Equal(2u, captured.PageSize);
        Assert.Equal(["a", "b"], page.Schedules.Select(s => s.Name));
        Assert.All(page.Schedules, s => Assert.False(s.SkipMissed));
        Assert.Equal("next", page.ContinuationToken);
    }

    [Fact]
    public async Task ListWorkflowSchedulesAsync_ShouldLeavePagingUnset_AndReturnNullToken_WhenLastPage()
    {
        ListWorkflowSchedulesRequest? captured = null;
        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.ListWorkflowSchedulesAsync(It.IsAny<ListWorkflowSchedulesRequest>(), It.IsAny<CallOptions>()))
            .Callback<ListWorkflowSchedulesRequest, CallOptions>((r, _) => captured = r)
            .Returns(CreateAsyncUnaryCall(new ListWorkflowSchedulesResponse()));
        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, new StubSerializer());

        var page = await client.ListWorkflowSchedulesAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(captured!.HasContinuationToken);
        Assert.False(captured.HasPageSize);
        Assert.Empty(page.Schedules);
        Assert.Null(page.ContinuationToken);
    }

    [Fact]
    public async Task DeleteWorkflowScheduleAsync_ShouldSendRequest()
    {
        DeleteWorkflowScheduleRequest? captured = null;
        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.DeleteWorkflowScheduleAsync(It.IsAny<DeleteWorkflowScheduleRequest>(), It.IsAny<CallOptions>()))
            .Callback<DeleteWorkflowScheduleRequest, CallOptions>((r, _) => captured = r)
            .Returns(CreateAsyncUnaryCall(new DeleteWorkflowScheduleResponse()));
        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, new StubSerializer());

        await client.DeleteWorkflowScheduleAsync("sched", TestContext.Current.CancellationToken);

        Assert.Equal("sched", captured!.Name);
    }

    [Fact]
    public async Task DeleteWorkflowScheduleAsync_ShouldPropagateNotFound()
    {
        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.DeleteWorkflowScheduleAsync(It.IsAny<DeleteWorkflowScheduleRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateAsyncUnaryCallThrows<DeleteWorkflowScheduleResponse>(
                new RpcException(new Status(StatusCode.NotFound, "not found"))));
        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, new StubSerializer());

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            client.DeleteWorkflowScheduleAsync("missing", TestContext.Current.CancellationToken));
        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task TriggerWorkflowScheduleAsync_ShouldReturnRunInstanceId()
    {
        TriggerWorkflowScheduleRequest? captured = null;
        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.TriggerWorkflowScheduleAsync(It.IsAny<TriggerWorkflowScheduleRequest>(), It.IsAny<CallOptions>()))
            .Callback<TriggerWorkflowScheduleRequest, CallOptions>((r, _) => captured = r)
            .Returns(CreateAsyncUnaryCall(new TriggerWorkflowScheduleResponse { InstanceId = "sched_20260101T000000Z_manual" }));
        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, new StubSerializer());

        var runId = await client.TriggerWorkflowScheduleAsync("sched", TestContext.Current.CancellationToken);

        Assert.Equal("sched", captured!.Name);
        Assert.Equal("sched_20260101T000000Z_manual", runId);
    }

    [Fact]
    public async Task GetInstanceHistoryAsync_ShouldThrowArgumentException_WhenInstanceIdIsNullOrEmpty()
    {
        var serializer = new StubSerializer();
        var grpcClientMock = CreateGrpcClientMock();
        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetInstanceHistoryAsync("", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetInstanceHistoryAsync_ShouldReturnConvertedHistoryEvents()
    {
        var serializer = new StubSerializer();
        GetInstanceHistoryRequest? captured = null;

        var timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
            new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc));

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.GetInstanceHistoryAsync(It.IsAny<GetInstanceHistoryRequest>(), It.IsAny<CallOptions>()))
            .Callback<GetInstanceHistoryRequest, CallOptions>((r, _) => captured = r)
            .Returns(CreateAsyncUnaryCall(new GetInstanceHistoryResponse
            {
                Events =
                {
                    new HistoryEvent
                    {
                        EventId = 1,
                        Timestamp = timestamp,
                        ExecutionStarted = new ExecutionStartedEvent { Name = "MyWorkflow" }
                    },
                    new HistoryEvent
                    {
                        EventId = 2,
                        Timestamp = timestamp,
                        TaskScheduled = new TaskScheduledEvent { Name = "MyActivity" }
                    }
                }
            }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var result = await client.GetInstanceHistoryAsync("i", TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal("i", captured!.InstanceId);
        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].EventId);
        Assert.Equal(WorkflowHistoryEventType.ExecutionStarted, result[0].EventType);
        Assert.Equal(2, result[1].EventId);
        Assert.Equal(WorkflowHistoryEventType.TaskScheduled, result[1].EventType);
    }

    [Fact]
    public async Task RerunWorkflowFromEventAsync_ShouldThrowArgumentException_WhenSourceInstanceIdIsNullOrEmpty()
    {
        var serializer = new StubSerializer();
        var grpcClientMock = CreateGrpcClientMock();
        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await Assert.ThrowsAsync<ArgumentException>(() => client.RerunWorkflowFromEventAsync("", 1, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RerunWorkflowFromEventAsync_ShouldSendRequestAndReturnNewInstanceId()
    {
        var serializer = new StubSerializer { SerializeResult = "{\"key\":\"value\"}" };
        RerunWorkflowFromEventRequest? captured = null;

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.RerunWorkflowFromEventAsync(It.IsAny<RerunWorkflowFromEventRequest>(), It.IsAny<CallOptions>()))
            .Callback<RerunWorkflowFromEventRequest, CallOptions>((r, _) => captured = r)
            .Returns(CreateAsyncUnaryCall(new RerunWorkflowFromEventResponse { NewInstanceID = "new-instance" }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var options = new RerunWorkflowFromEventOptions
        {
            NewInstanceId = "custom-id",
            Input = new { Key = "value" },
            OverwriteInput = true
        };

        var result = await client.RerunWorkflowFromEventAsync("source-id", 42, options, TestContext.Current.CancellationToken);

        Assert.Equal("new-instance", result);
        Assert.NotNull(captured);
        Assert.Equal("source-id", captured!.SourceInstanceID);
        Assert.Equal(42u, captured.EventID);
        Assert.Equal("custom-id", captured.NewInstanceID);
        Assert.True(captured.OverwriteInput);
        Assert.Equal("{\"key\":\"value\"}", captured.Input);
    }

    [Fact]
    public async Task RerunWorkflowFromEventAsync_ShouldNotSetOptionalFields_WhenOptionsIsNull()
    {
        var serializer = new StubSerializer();
        RerunWorkflowFromEventRequest? captured = null;

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.RerunWorkflowFromEventAsync(It.IsAny<RerunWorkflowFromEventRequest>(), It.IsAny<CallOptions>()))
            .Callback<RerunWorkflowFromEventRequest, CallOptions>((r, _) => captured = r)
            .Returns(CreateAsyncUnaryCall(new RerunWorkflowFromEventResponse { NewInstanceID = "auto-id" }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var result = await client.RerunWorkflowFromEventAsync("source-id", 5, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("auto-id", result);
        Assert.NotNull(captured);
        Assert.Equal("source-id", captured!.SourceInstanceID);
        Assert.Equal(5u, captured.EventID);
        Assert.False(captured.HasNewInstanceID);
        Assert.False(captured.OverwriteInput);
        Assert.Null(captured.Input);
    }

    [Fact]
    public async Task DisposeAsync_ShouldCompleteSynchronously()
    {
        var serializer = new StubSerializer();
        var grpcClientMock = CreateGrpcClientMock();

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        await client.DisposeAsync();
    }
    
    [Theory]
    [InlineData(OrchestrationStatus.Failed, WorkflowRuntimeStatus.Failed)]
    [InlineData(OrchestrationStatus.Terminated, WorkflowRuntimeStatus.Terminated)]
    public async Task WaitForWorkflowCompletionAsync_ShouldMapTerminalStatus_ForFailedOrTerminated(
        OrchestrationStatus protoStatus,
        WorkflowRuntimeStatus expectedStatus)
    {
        var serializer = new JsonDaprSerializer();

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.WaitForInstanceCompletionAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateAsyncUnaryCall(new GetInstanceResponse
            {
                Exists = true,
                WorkflowState = new Dapr.DurableTask.Protobuf.WorkflowState
                {
                    InstanceId = "i",
                    Name = "n",
                    WorkflowStatus = protoStatus
                }
            }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var result = await client.WaitForWorkflowCompletionAsync("i", getInputsAndOutputs: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("i", result.InstanceId);
        Assert.Equal(expectedStatus, result.RuntimeStatus);
    }

    [Fact]
    public async Task WaitForWorkflowCompletionAsync_ShouldThrowInvalidOperationException_WhenInstanceDoesNotExist()
    {
        var serializer = new JsonDaprSerializer();

        var grpcClientMock = CreateGrpcClientMock();
        grpcClientMock
            .Setup(x => x.WaitForInstanceCompletionAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()))
            .Returns(CreateAsyncUnaryCall(new GetInstanceResponse { Exists = false }));

        var client = new WorkflowGrpcClient(grpcClientMock.Object, NullLogger<WorkflowGrpcClient>.Instance, serializer);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.WaitForWorkflowCompletionAsync("missing", getInputsAndOutputs: true, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("missing", ex.Message);
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

        public string Serialize<T>(T value) => value is null ? string.Empty : SerializeResult;

        public string Serialize(object? value, Type? inputType = null) => value is null ? string.Empty : SerializeResult;

        public T Deserialize<T>(string? data) => throw new NotSupportedException();

        public object Deserialize(string? data, Type returnType) => throw new NotSupportedException();
    }
}
