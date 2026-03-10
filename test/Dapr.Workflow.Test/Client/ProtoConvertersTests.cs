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

using System.Text.Json;
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Client;
using Dapr.Workflow.Serialization;
using Google.Protobuf.WellKnownTypes;

namespace Dapr.Workflow.Test.Client;

public class ProtoConvertersTests
{
    [Theory]
    [InlineData(OrchestrationStatus.Running, WorkflowRuntimeStatus.Running)]
    [InlineData(OrchestrationStatus.Completed, WorkflowRuntimeStatus.Completed)]
    [InlineData(OrchestrationStatus.ContinuedAsNew, WorkflowRuntimeStatus.ContinuedAsNew)]
    [InlineData(OrchestrationStatus.Failed, WorkflowRuntimeStatus.Failed)]
    [InlineData(OrchestrationStatus.Canceled, WorkflowRuntimeStatus.Canceled)]
    [InlineData(OrchestrationStatus.Terminated, WorkflowRuntimeStatus.Terminated)]
    [InlineData(OrchestrationStatus.Pending, WorkflowRuntimeStatus.Pending)]
    [InlineData(OrchestrationStatus.Suspended, WorkflowRuntimeStatus.Suspended)]
    [InlineData(OrchestrationStatus.Stalled, WorkflowRuntimeStatus.Stalled)]
    public void ToRuntimeStatus_ShouldMapKnownProtoValues_ToExpectedWorkflowRuntimeStatus(
        OrchestrationStatus protoStatus,
        WorkflowRuntimeStatus expected)
    {
        var actual = ProtoConverters.ToRuntimeStatus(protoStatus);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ToRuntimeStatus_ShouldReturnUnknown_WhenProtoStatusIsUnrecognized()
    {
        var actual = ProtoConverters.ToRuntimeStatus((OrchestrationStatus)9999);

        Assert.Equal(WorkflowRuntimeStatus.Unknown, actual);
    }

    [Fact]
    public void ToWorkflowMetadata_ShouldMapCoreFields_AndPreserveSerializerInstance()
    {
        var serializer = new JsonWorkflowSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var createdUtc = new DateTime(2025, 01, 02, 03, 04, 05, DateTimeKind.Utc);
        var updatedUtc = new DateTime(2025, 02, 03, 04, 05, 06, DateTimeKind.Utc);

        var state = new OrchestrationState
        {
            InstanceId = "instance-123",
            Name = "MyWorkflow",
            OrchestrationStatus = OrchestrationStatus.Running,
            CreatedTimestamp = Timestamp.FromDateTime(createdUtc),
            LastUpdatedTimestamp = Timestamp.FromDateTime(updatedUtc),
            Input = "{\"hello\":\"in\"}",
            Output = "{\"hello\":\"out\"}",
            CustomStatus = "{\"hello\":\"status\"}"
        };

        var metadata = ProtoConverters.ToWorkflowMetadata(state, serializer);

        Assert.Equal("instance-123", metadata.InstanceId);
        Assert.Equal("MyWorkflow", metadata.Name);
        Assert.Equal(WorkflowRuntimeStatus.Running, metadata.RuntimeStatus);
        Assert.Equal(createdUtc, metadata.CreatedAt);
        Assert.Equal(updatedUtc, metadata.LastUpdatedAt);

        Assert.Equal("{\"hello\":\"in\"}", metadata.SerializedInput);
        Assert.Equal("{\"hello\":\"out\"}", metadata.SerializedOutput);
        Assert.Equal("{\"hello\":\"status\"}", metadata.SerializedCustomStatus);

        Assert.Same(serializer, metadata.Serializer);
    }

    [Fact]
    public void ToWorkflowMetadata_ShouldSetMinValueTimestamps_WhenProtoTimestampsAreNull()
    {
        var serializer = new JsonWorkflowSerializer();

        var state = new OrchestrationState
        {
            InstanceId = "i",
            Name = "n",
            OrchestrationStatus = OrchestrationStatus.Pending,
            CreatedTimestamp = null,
            LastUpdatedTimestamp = null
        };

        var metadata = ProtoConverters.ToWorkflowMetadata(state, serializer);

        Assert.Equal(DateTime.MinValue, metadata.CreatedAt);
        Assert.Equal(DateTime.MinValue, metadata.LastUpdatedAt);
    }

    [Fact]
    public void ToWorkflowMetadata_ShouldSetSerializedFieldsToNull_WhenProtoStringsAreNullOrEmpty()
    {
        var serializer = new JsonWorkflowSerializer();

        var state = new OrchestrationState
        {
            InstanceId = "i",
            Name = "n",
            OrchestrationStatus = OrchestrationStatus.Completed,
            Input = "",
            Output = null,
            CustomStatus = ""
        };

        var metadata = ProtoConverters.ToWorkflowMetadata(state, serializer);

        Assert.Null(metadata.SerializedInput);
        Assert.Null(metadata.SerializedOutput);
        Assert.Null(metadata.SerializedCustomStatus);
    }

    [Fact]
    public void ToWorkflowMetadata_ShouldKeepSerializedFields_WhenProtoStringsContainWhitespace()
    {
        var serializer = new JsonWorkflowSerializer();

        var state = new OrchestrationState
        {
            InstanceId = "i",
            Name = "n",
            OrchestrationStatus = OrchestrationStatus.Completed,
            Input = " ",
            Output = "\t",
            CustomStatus = "\r\n"
        };

        var metadata = ProtoConverters.ToWorkflowMetadata(state, serializer);

        Assert.Equal(" ", metadata.SerializedInput);
        Assert.Equal("\t", metadata.SerializedOutput);
        Assert.Equal("\r\n", metadata.SerializedCustomStatus);
    }

    [Fact]
    public void ToWorkflowMetadata_ShouldMapRuntimeStatus_UsingToRuntimeStatus()
    {
        var serializer = new JsonWorkflowSerializer();

        var state = new OrchestrationState
        {
            InstanceId = "i",
            Name = "n",
            OrchestrationStatus = OrchestrationStatus.ContinuedAsNew
        };

        var metadata = ProtoConverters.ToWorkflowMetadata(state, serializer);

        Assert.Equal(WorkflowRuntimeStatus.ContinuedAsNew, metadata.RuntimeStatus);
    }

    [Theory]
    [InlineData(HistoryEvent.EventTypeOneofCase.ExecutionStarted, WorkflowHistoryEventType.ExecutionStarted)]
    [InlineData(HistoryEvent.EventTypeOneofCase.ExecutionCompleted, WorkflowHistoryEventType.ExecutionCompleted)]
    [InlineData(HistoryEvent.EventTypeOneofCase.ExecutionTerminated, WorkflowHistoryEventType.ExecutionTerminated)]
    [InlineData(HistoryEvent.EventTypeOneofCase.TaskScheduled, WorkflowHistoryEventType.TaskScheduled)]
    [InlineData(HistoryEvent.EventTypeOneofCase.TaskCompleted, WorkflowHistoryEventType.TaskCompleted)]
    [InlineData(HistoryEvent.EventTypeOneofCase.TaskFailed, WorkflowHistoryEventType.TaskFailed)]
    [InlineData(HistoryEvent.EventTypeOneofCase.TimerCreated, WorkflowHistoryEventType.TimerCreated)]
    [InlineData(HistoryEvent.EventTypeOneofCase.TimerFired, WorkflowHistoryEventType.TimerFired)]
    [InlineData(HistoryEvent.EventTypeOneofCase.EventRaised, WorkflowHistoryEventType.EventRaised)]
    [InlineData(HistoryEvent.EventTypeOneofCase.ExecutionSuspended, WorkflowHistoryEventType.ExecutionSuspended)]
    [InlineData(HistoryEvent.EventTypeOneofCase.ExecutionResumed, WorkflowHistoryEventType.ExecutionResumed)]
    public void ToHistoryEventType_ShouldMapKnownEventTypes(
        HistoryEvent.EventTypeOneofCase protoEventType,
        WorkflowHistoryEventType expected)
    {
        var actual = ProtoConverters.ToHistoryEventType(protoEventType);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ToHistoryEventType_ShouldReturnUnknown_WhenEventTypeIsNone()
    {
        var actual = ProtoConverters.ToHistoryEventType(HistoryEvent.EventTypeOneofCase.None);

        Assert.Equal(WorkflowHistoryEventType.Unknown, actual);
    }

    [Fact]
    public void ToWorkflowHistoryEvent_ShouldMapAllFields()
    {
        var timestamp = Timestamp.FromDateTime(new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc));

        var protoEvent = new HistoryEvent
        {
            EventId = 42,
            Timestamp = timestamp,
            ExecutionStarted = new ExecutionStartedEvent { Name = "MyWorkflow" }
        };

        var result = ProtoConverters.ToWorkflowHistoryEvent(protoEvent);

        Assert.Equal(42, result.EventId);
        Assert.Equal(WorkflowHistoryEventType.ExecutionStarted, result.EventType);
        Assert.Equal(new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc), result.Timestamp);
    }

    [Fact]
    public void ToWorkflowHistoryEvent_ShouldUseMinValue_WhenTimestampIsNull()
    {
        var protoEvent = new HistoryEvent
        {
            EventId = 1,
            TaskScheduled = new TaskScheduledEvent { Name = "MyActivity" }
        };

        var result = ProtoConverters.ToWorkflowHistoryEvent(protoEvent);

        Assert.Equal(DateTime.MinValue, result.Timestamp);
    }
}
