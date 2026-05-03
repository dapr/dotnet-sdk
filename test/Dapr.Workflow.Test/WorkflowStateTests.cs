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

using Dapr.Workflow.Client;
using Dapr.Workflow.Serialization;

namespace Dapr.Workflow.Test;

public class WorkflowStateTests
{
    [Fact]
    public void Properties_ShouldReturnDefaults_WhenMetadataIsNull()
    {
        var state = new WorkflowState(null);

        Assert.False(state.Exists);
        Assert.False(state.IsWorkflowRunning);
        Assert.False(state.IsWorkflowCompleted);
        Assert.Equal(DateTime.MinValue, state.CreatedAt.DateTime);
        Assert.Equal(DateTime.MinValue, state.LastUpdatedAt.DateTime);
        Assert.Equal(WorkflowRuntimeStatus.Unknown, state.RuntimeStatus);
        Assert.Null(state.FailureDetails);

        Assert.Equal(default, state.ReadInputAs<int>());
        Assert.Equal(default, state.ReadOutputAs<int>());
        Assert.Equal(default, state.ReadCustomStatusAs<int>());
    }

    [Fact]
    public void Properties_ShouldReflectMetadata_WhenPresent()
    {
        var serializer = new JsonWorkflowSerializer();
        var created = new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc);
        var updated = new DateTime(2025, 01, 02, 0, 0, 0, DateTimeKind.Utc);

        var metadata = new WorkflowMetadata(
            InstanceId: "i",
            Name: "wf",
            RuntimeStatus: WorkflowRuntimeStatus.Running,
            CreatedAt: created,
            LastUpdatedAt: updated,
            Serializer: serializer);

        var state = new WorkflowState(metadata);

        Assert.True(state.Exists);
        Assert.True(state.IsWorkflowRunning);
        Assert.False(state.IsWorkflowCompleted);
        Assert.Equal(created, state.CreatedAt.DateTime);
        Assert.Equal(updated, state.LastUpdatedAt.DateTime);
        Assert.Equal(WorkflowRuntimeStatus.Running, state.RuntimeStatus);
    }

    [Fact]
    public void CreatedAt_ShouldReturnDefault_WhenMetadataCreatedAtIsMinValue()
    {
        var serializer = new JsonWorkflowSerializer();
        var metadata = new WorkflowMetadata(
            InstanceId: "i",
            Name: "wf",
            RuntimeStatus: WorkflowRuntimeStatus.Running,
            CreatedAt: DateTime.MinValue,
            LastUpdatedAt: DateTime.MinValue,
            Serializer: serializer);

        var state = new WorkflowState(metadata);

        Assert.Equal(DateTime.MinValue, state.CreatedAt.DateTime);
        Assert.Equal(DateTime.MinValue, state.LastUpdatedAt.DateTime);
    }

    [Theory]
    [InlineData(WorkflowRuntimeStatus.Completed)]
    [InlineData(WorkflowRuntimeStatus.Failed)]
    [InlineData(WorkflowRuntimeStatus.Terminated)]
    public void IsWorkflowCompleted_ShouldBeTrue_ForTerminalStatuses(WorkflowRuntimeStatus status)
    {
        var serializer = new JsonWorkflowSerializer();
        var metadata = new WorkflowMetadata("i", "wf", status, DateTime.MinValue, DateTime.MinValue, serializer);

        var state = new WorkflowState(metadata);

        Assert.True(state.IsWorkflowCompleted);
    }

    [Fact]
    public void DerivedType_CanOverrideAllProperties()
    {
        var failureDetails = new WorkflowTaskFailureDetails("err", "msg");
        var created = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var updated = new DateTimeOffset(2025, 6, 2, 0, 0, 0, TimeSpan.Zero);

        var state = new TestWorkflowState
        {
            ExistsValue = true,
            IsWorkflowRunningValue = true,
            IsWorkflowCompletedValue = false,
            CreatedAtValue = created,
            LastUpdatedAtValue = updated,
            RuntimeStatusValue = WorkflowRuntimeStatus.Running,
            FailureDetailsValue = failureDetails
        };

        Assert.True(state.Exists);
        Assert.True(state.IsWorkflowRunning);
        Assert.False(state.IsWorkflowCompleted);
        Assert.Equal(created, state.CreatedAt);
        Assert.Equal(updated, state.LastUpdatedAt);
        Assert.Equal(WorkflowRuntimeStatus.Running, state.RuntimeStatus);
        Assert.Same(failureDetails, state.FailureDetails);
        Assert.Equal(42, state.ReadInputAs<int>());
        Assert.Equal("output", state.ReadOutputAs<string>());
        Assert.Equal("custom", state.ReadCustomStatusAs<string>());
    }

    [Fact]
    public void DerivedType_DefaultValues_WhenProtectedConstructorUsed()
    {
        var state = new MinimalTestWorkflowState();

        Assert.False(state.Exists);
        Assert.False(state.IsWorkflowRunning);
        Assert.False(state.IsWorkflowCompleted);
        Assert.Equal(default, state.CreatedAt);
        Assert.Equal(default, state.LastUpdatedAt);
        Assert.Equal(WorkflowRuntimeStatus.Unknown, state.RuntimeStatus);
        Assert.Null(state.FailureDetails);
        Assert.Equal(default, state.ReadInputAs<int>());
        Assert.Null(state.ReadOutputAs<string>());
        Assert.Null(state.ReadCustomStatusAs<string>());
    }

    private sealed class TestWorkflowState : WorkflowState
    {
        public bool ExistsValue { get; set; }
        public bool IsWorkflowRunningValue { get; set; }
        public bool IsWorkflowCompletedValue { get; set; }
        public DateTimeOffset CreatedAtValue { get; set; }
        public DateTimeOffset LastUpdatedAtValue { get; set; }
        public WorkflowRuntimeStatus RuntimeStatusValue { get; set; } = WorkflowRuntimeStatus.Unknown;
        public WorkflowTaskFailureDetails? FailureDetailsValue { get; set; }

        public override bool Exists => ExistsValue;
        public override bool IsWorkflowRunning => IsWorkflowRunningValue;
        public override bool IsWorkflowCompleted => IsWorkflowCompletedValue;
        public override DateTimeOffset CreatedAt => CreatedAtValue;
        public override DateTimeOffset LastUpdatedAt => LastUpdatedAtValue;
        public override WorkflowRuntimeStatus RuntimeStatus => RuntimeStatusValue;
        public override WorkflowTaskFailureDetails? FailureDetails => FailureDetailsValue;
        public override T? ReadInputAs<T>() where T : default => (T?)(object?)42;
        public override T? ReadOutputAs<T>() where T : default => (T?)(object?)"output";
        public override T? ReadCustomStatusAs<T>() where T : default => (T?)(object?)"custom";
    }

    /// <summary>
    /// A minimal derived type that does not override any members, exercising the base class defaults
    /// via the protected parameterless constructor.
    /// </summary>
    private sealed class MinimalTestWorkflowState : WorkflowState;
}
