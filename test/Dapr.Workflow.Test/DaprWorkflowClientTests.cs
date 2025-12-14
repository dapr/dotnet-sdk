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
// ------------------------------------------------------------------------

using Dapr.Workflow.Client;

namespace Dapr.Workflow.Test;

public class DaprWorkflowClientTests
{
    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenInnerClientIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new DaprWorkflowClient(null!));
    }

    [Fact]
    public async Task ScheduleNewWorkflowAsync_ShouldThrowArgumentException_WhenNameIsNullOrEmpty()
    {
        var inner = new CapturingWorkflowClient();
        var client = new DaprWorkflowClient(inner);

        await Assert.ThrowsAsync<ArgumentException>(() => client.ScheduleNewWorkflowAsync(""));
    }

    [Fact]
    public async Task ScheduleNewWorkflowAsync_ShouldForwardToInnerClient_WithStartOptionsAndCancellationToken()
    {
        var inner = new CapturingWorkflowClient { ScheduleNewWorkflowResult = "returned-id" };
        var client = new DaprWorkflowClient(inner);

        using var cts = new CancellationTokenSource();
        var startAt = new DateTimeOffset(2025, 01, 02, 03, 04, 05, TimeSpan.Zero);

        var instanceId = await client.ScheduleNewWorkflowAsync(
            name: "MyWorkflow",
            instanceId: "instance-123",
            input: new { A = 1 },
            startTime: startAt,
            cancellation: cts.Token);

        Assert.Equal("returned-id", instanceId);

        Assert.Equal("MyWorkflow", inner.LastScheduleName);
        Assert.NotNull(inner.LastScheduleOptions);
        Assert.Equal("instance-123", inner.LastScheduleOptions!.InstanceId);
        Assert.Equal(startAt, inner.LastScheduleOptions.StartAt);
        Assert.Equal(cts.Token, inner.LastScheduleCancellationToken);
        Assert.NotNull(inner.LastScheduleInput);
    }

    [Fact]
    public async Task ScheduleNewWorkflowAsync_DateTimeOverload_ShouldConvertToDateTimeOffset_WhenStartTimeProvided()
    {
        var inner = new CapturingWorkflowClient { ScheduleNewWorkflowResult = "id" };
        var client = new DaprWorkflowClient(inner);

        var start = new DateTime(2025, 07, 10, 1, 2, 3, DateTimeKind.Utc);

        await client.ScheduleNewWorkflowAsync("wf", "i", input: null, startTime: start);

        Assert.NotNull(inner.LastScheduleOptions);
        Assert.NotNull(inner.LastScheduleOptions!.StartAt);
        Assert.Equal(new DateTimeOffset(start), inner.LastScheduleOptions.StartAt);
    }

    [Fact]
    public async Task GetWorkflowStateAsync_ShouldThrowArgumentException_WhenInstanceIdIsNullOrEmpty()
    {
        var inner = new CapturingWorkflowClient();
        var client = new DaprWorkflowClient(inner);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetWorkflowStateAsync(""));
    }

    [Fact]
    public async Task GetWorkflowStateAsync_ShouldReturnNull_WhenInnerReturnsNullMetadata()
    {
        var inner = new CapturingWorkflowClient { GetWorkflowMetadataResult = null };
        var client = new DaprWorkflowClient(inner);

        var state = await client.GetWorkflowStateAsync("missing");

        Assert.Null(state);
        Assert.Equal("missing", inner.LastGetMetadataInstanceId);
        Assert.True(inner.LastGetMetadataGetInputsAndOutputs);
    }

    [Fact]
    public async Task GetWorkflowStateAsync_ShouldReturnWorkflowState_WhenInnerReturnsMetadata()
    {
        var metadata = new WorkflowMetadata(
            InstanceId: "i",
            Name: "wf",
            RuntimeStatus: WorkflowRuntimeStatus.Running,
            CreatedAt: DateTime.MinValue,
            LastUpdatedAt: DateTime.MinValue,
            Serializer: new Serialization.JsonWorkflowSerializer());

        var inner = new CapturingWorkflowClient { GetWorkflowMetadataResult = metadata };
        var client = new DaprWorkflowClient(inner);

        var state = await client.GetWorkflowStateAsync("i");

        Assert.NotNull(state);
        Assert.True(state!.Exists);
        Assert.True(state.IsWorkflowRunning);
        Assert.Equal(WorkflowRuntimeStatus.Running, state.RuntimeStatus);
    }

    [Fact]
    public async Task WaitForWorkflowStartAsync_ShouldThrowArgumentException_WhenInstanceIdIsNullOrEmpty()
    {
        var inner = new CapturingWorkflowClient();
        var client = new DaprWorkflowClient(inner);

        await Assert.ThrowsAsync<ArgumentException>(() => client.WaitForWorkflowStartAsync(""));
    }

    [Fact]
    public async Task WaitForWorkflowStartAsync_ShouldWrapMetadataIntoWorkflowState()
    {
        var metadata = new WorkflowMetadata(
            InstanceId: "i",
            Name: "wf",
            RuntimeStatus: WorkflowRuntimeStatus.Running,
            CreatedAt: DateTime.MinValue,
            LastUpdatedAt: DateTime.MinValue,
            Serializer: new Serialization.JsonWorkflowSerializer());

        var inner = new CapturingWorkflowClient { WaitForStartResult = metadata };
        var client = new DaprWorkflowClient(inner);

        var state = await client.WaitForWorkflowStartAsync("i", getInputsAndOutputs: false);

        Assert.True(state.Exists);
        Assert.Equal(WorkflowRuntimeStatus.Running, state.RuntimeStatus);
        Assert.Equal("i", inner.LastWaitForStartInstanceId);
        Assert.False(inner.LastWaitForStartGetInputsAndOutputs);
    }

    [Fact]
    public async Task WaitForWorkflowCompletionAsync_ShouldThrowArgumentException_WhenInstanceIdIsNullOrEmpty()
    {
        var inner = new CapturingWorkflowClient();
        var client = new DaprWorkflowClient(inner);

        await Assert.ThrowsAsync<ArgumentException>(() => client.WaitForWorkflowCompletionAsync(""));
    }

    [Fact]
    public async Task WaitForWorkflowCompletionAsync_ShouldWrapMetadataIntoWorkflowState()
    {
        var metadata = new WorkflowMetadata(
            InstanceId: "i",
            Name: "wf",
            RuntimeStatus: WorkflowRuntimeStatus.Completed,
            CreatedAt: DateTime.MinValue,
            LastUpdatedAt: DateTime.MinValue,
            Serializer: new Serialization.JsonWorkflowSerializer())
        {
            SerializedOutput = "\"done\""
        };

        var inner = new CapturingWorkflowClient { WaitForCompletionResult = metadata };
        var client = new DaprWorkflowClient(inner);

        var state = await client.WaitForWorkflowCompletionAsync("i");

        Assert.True(state.Exists);
        Assert.True(state.IsWorkflowCompleted);
        Assert.Equal(WorkflowRuntimeStatus.Completed, state.RuntimeStatus);
    }

    [Fact]
    public async Task RaiseEventAsync_ShouldValidateParameters_AndForwardToInner()
    {
        var inner = new CapturingWorkflowClient();
        var client = new DaprWorkflowClient(inner);

        await Assert.ThrowsAsync<ArgumentException>(() => client.RaiseEventAsync("", "evt"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.RaiseEventAsync("i", ""));

        await client.RaiseEventAsync("i", "evt", new { P = 1 });

        Assert.Equal("i", inner.LastRaiseEventInstanceId);
        Assert.Equal("evt", inner.LastRaiseEventName);
        Assert.NotNull(inner.LastRaiseEventPayload);
    }

    [Fact]
    public async Task TerminateSuspendResumePurge_ShouldValidateInstanceId_AndForwardToInner()
    {
        var inner = new CapturingWorkflowClient { PurgeResult = true };
        var client = new DaprWorkflowClient(inner);

        await Assert.ThrowsAsync<ArgumentException>(() => client.TerminateWorkflowAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => client.SuspendWorkflowAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => client.ResumeWorkflowAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => client.PurgeInstanceAsync(""));

        await client.TerminateWorkflowAsync("i", output: "o");
        await client.SuspendWorkflowAsync("i", reason: "r1");
        await client.ResumeWorkflowAsync("i", reason: "r2");
        var purged = await client.PurgeInstanceAsync("i");

        Assert.Equal("i", inner.LastTerminateInstanceId);
        Assert.Equal("i", inner.LastSuspendInstanceId);
        Assert.Equal("i", inner.LastResumeInstanceId);
        Assert.Equal("i", inner.LastPurgeInstanceId);
        Assert.True(purged);
    }

    [Fact]
    public async Task DisposeAsync_ShouldForwardToInner()
    {
        var inner = new CapturingWorkflowClient();
        var client = new DaprWorkflowClient(inner);

        await client.DisposeAsync();

        Assert.True(inner.DisposeCalled);
    }

    private sealed class CapturingWorkflowClient : WorkflowClient
    {
        public string? LastScheduleName { get; private set; }
        public object? LastScheduleInput { get; private set; }
        public StartWorkflowOptions? LastScheduleOptions { get; private set; }
        public CancellationToken LastScheduleCancellationToken { get; private set; }
        public string ScheduleNewWorkflowResult { get; set; } = "id";

        public string? LastGetMetadataInstanceId { get; private set; }
        public bool LastGetMetadataGetInputsAndOutputs { get; private set; }
        public WorkflowMetadata? GetWorkflowMetadataResult { get; set; }

        public string? LastWaitForStartInstanceId { get; private set; }
        public bool LastWaitForStartGetInputsAndOutputs { get; private set; }
        public WorkflowMetadata WaitForStartResult { get; set; } =
            new("i", "wf", WorkflowRuntimeStatus.Running, DateTime.MinValue, DateTime.MinValue, new Serialization.JsonWorkflowSerializer());

        public WorkflowMetadata WaitForCompletionResult { get; set; } =
            new("i", "wf", WorkflowRuntimeStatus.Completed, DateTime.MinValue, DateTime.MinValue, new Serialization.JsonWorkflowSerializer());

        public string? LastRaiseEventInstanceId { get; private set; }
        public string? LastRaiseEventName { get; private set; }
        public object? LastRaiseEventPayload { get; private set; }

        public string? LastTerminateInstanceId { get; private set; }
        public object? LastTerminateOutput { get; private set; }

        public string? LastSuspendInstanceId { get; private set; }
        public string? LastSuspendReason { get; private set; }

        public string? LastResumeInstanceId { get; private set; }
        public string? LastResumeReason { get; private set; }

        public string? LastPurgeInstanceId { get; private set; }
        public bool PurgeResult { get; set; }

        public bool DisposeCalled { get; private set; }

        public override Task<string> ScheduleNewWorkflowAsync(
            string workflowName,
            object? input = null,
            StartWorkflowOptions? options = null,
            CancellationToken cancellation = default)
        {
            LastScheduleName = workflowName;
            LastScheduleInput = input;
            LastScheduleOptions = options;
            LastScheduleCancellationToken = cancellation;
            return Task.FromResult(ScheduleNewWorkflowResult);
        }

        public override Task<WorkflowMetadata?> GetWorkflowMetadataAsync(
            string instanceId,
            bool getInputsAndOutputs = true,
            CancellationToken cancellationToken = default)
        {
            LastGetMetadataInstanceId = instanceId;
            LastGetMetadataGetInputsAndOutputs = getInputsAndOutputs;
            return Task.FromResult(GetWorkflowMetadataResult);
        }

        public override Task<WorkflowMetadata> WaitForWorkflowStartAsync(
            string instanceId,
            bool getInputsAndOutputs = true,
            CancellationToken cancellationToken = default)
        {
            LastWaitForStartInstanceId = instanceId;
            LastWaitForStartGetInputsAndOutputs = getInputsAndOutputs;
            return Task.FromResult(WaitForStartResult);
        }

        public override Task<WorkflowMetadata> WaitForWorkflowCompletionAsync(
            string instanceId,
            bool getInputsAndOutputs = true,
            CancellationToken cancellationToken = default)
            => Task.FromResult(WaitForCompletionResult);

        public override Task RaiseEventAsync(
            string instanceId,
            string eventName,
            object? eventPayload = null,
            CancellationToken cancellationToken = default)
        {
            LastRaiseEventInstanceId = instanceId;
            LastRaiseEventName = eventName;
            LastRaiseEventPayload = eventPayload;
            return Task.CompletedTask;
        }

        public override Task TerminateWorkflowAsync(
            string instanceId,
            object? output = null,
            CancellationToken cancellationToken = default)
        {
            LastTerminateInstanceId = instanceId;
            LastTerminateOutput = output;
            return Task.CompletedTask;
        }

        public override Task SuspendWorkflowAsync(
            string instanceId,
            string? reason = null,
            CancellationToken cancellationToken = default)
        {
            LastSuspendInstanceId = instanceId;
            LastSuspendReason = reason;
            return Task.CompletedTask;
        }

        public override Task ResumeWorkflowAsync(
            string instanceId,
            string? reason = null,
            CancellationToken cancellationToken = default)
        {
            LastResumeInstanceId = instanceId;
            LastResumeReason = reason;
            return Task.CompletedTask;
        }

        public override Task<bool> PurgeInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
        {
            LastPurgeInstanceId = instanceId;
            return Task.FromResult(PurgeResult);
        }

        public override ValueTask DisposeAsync()
        {
            DisposeCalled = true;
            return ValueTask.CompletedTask;
        }
    }
    
    
//     [Fact]
//     public async Task ScheduleNewWorkflowAsync_DateTimeKindUnspecified_AssumesLocalTime()
//     {
//         var innerClient = new Mock<GrpcDurableTaskClientWrapper>();
//
//         var name = "test-workflow";
//         var instanceId = "test-instance-id";
//         var input = "test-input";
//         var startTime = new DateTime(2025, 07, 10);
//
//         Assert.Equal(DateTimeKind.Unspecified, startTime.Kind);
//
//         innerClient.Setup(c => c.ScheduleNewOrchestrationInstanceAsync(
//             It.IsAny<TaskName>(),
//             It.IsAny<object?>(),
//             It.IsAny<StartOrchestrationOptions?>(),
//             It.IsAny<CancellationToken>()))
//             .Callback((TaskName n, object? i, StartOrchestrationOptions? o, CancellationToken ct) =>
//             {
//                 Assert.Equal(name, n);
//                 Assert.Equal(input, i);
//                 Assert.NotNull(o);
//                 Assert.NotNull(o.StartAt);
//                 // options configured with local time
//                 Assert.Equal(new DateTimeOffset(startTime, TimeZoneInfo.Local.GetUtcOffset(DateTime.Now)), o.StartAt.Value);
//             })
//             .ReturnsAsync("instance-id");
//
//         var client = new DaprWorkflowClient(innerClient.Object);
//
//         await client.ScheduleNewWorkflowAsync(name, instanceId, input, startTime);
//     }
//
//     [Fact]
//     public async Task ScheduleNewWorkflowAsync_DateTimeKindUtc_PreservedAsUtc()
//     {
//         var innerClient = new Mock<GrpcDurableTaskClientWrapper>();
//
//         var name = "test-workflow";
//         var instanceId = "test-instance-id";
//         var input = "test-input";
//         var startTime = new DateTime(2025, 07, 10, 1, 30, 30, DateTimeKind.Utc);
//
//         Assert.Equal(DateTimeKind.Utc, startTime.Kind);
//
//         innerClient.Setup(c => c.ScheduleNewOrchestrationInstanceAsync(
//             It.IsAny<TaskName>(),
//             It.IsAny<object?>(),
//             It.IsAny<StartOrchestrationOptions?>(),
//             It.IsAny<CancellationToken>()))
//             .Callback((TaskName n, object? i, StartOrchestrationOptions? o, CancellationToken ct) =>
//             {
//                 Assert.Equal(name, n);
//                 Assert.Equal(input, i);
//                 Assert.NotNull(o);
//                 Assert.NotNull(o.StartAt);
//                 // options configured with UTC time
//                 Assert.Equal(new DateTimeOffset(startTime, TimeSpan.Zero), o.StartAt.Value);
//             })
//             .ReturnsAsync("instance-id");
//
//         var client = new DaprWorkflowClient(innerClient.Object);
//
//         await client.ScheduleNewWorkflowAsync(name, instanceId, input, startTime);
//     }
//
//     [Fact]
//     public async Task ScheduleNewWorkflowAsync_DateTimeOffset_SetsStartAt()
//     {
//         var innerClient = new Mock<GrpcDurableTaskClientWrapper>();
//
//         var name = "test-workflow";
//         var instanceId = "test-instance-id";
//         var input = "test-input";
//         var startTime = new DateTimeOffset(2025, 07, 10, 1, 30, 30, TimeSpan.FromHours(3));
//
//         innerClient.Setup(c => c.ScheduleNewOrchestrationInstanceAsync(
//             It.IsAny<TaskName>(),
//             It.IsAny<object?>(),
//             It.IsAny<StartOrchestrationOptions?>(),
//             It.IsAny<CancellationToken>()))
//             .Callback((TaskName n, object? i, StartOrchestrationOptions? o, CancellationToken ct) =>
//             {
//                 Assert.Equal(name, n);
//                 Assert.Equal(input, i);
//                 Assert.NotNull(o);
//                 Assert.NotNull(o.StartAt);
//                 // options configured with specified offset
//                 Assert.Equal(startTime, o.StartAt.Value);
//             })
//             .ReturnsAsync("instance-id");
//
//         var client = new DaprWorkflowClient(innerClient.Object);
//
//         await client.ScheduleNewWorkflowAsync(name, instanceId, input, startTime);
//     }
}
