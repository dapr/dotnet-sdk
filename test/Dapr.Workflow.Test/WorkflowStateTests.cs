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
        Assert.Equal(created, state.CreatedAt);
        Assert.Equal(updated, state.LastUpdatedAt);
        Assert.Equal(WorkflowRuntimeStatus.Running, state.RuntimeStatus);
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
}
