// ------------------------------------------------------------------------
// Copyright 2023 The Dapr Authors
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

namespace Dapr.Workflow;

/// <summary>
/// Options that can be used to control the behavior of workflow task execution.
/// </summary>
/// <param name="RetryPolicy">The workflow retry policy.</param>
/// <param name="TargetAppId">The App ID indicating the app in which to find the named activity to run.</param>
/// <param name="PropagationScope">
/// Determines which ancestor history events are propagated to the scheduled activity or child workflow.
/// Defaults to <see cref="HistoryPropagationScope.None"/>, meaning no history is propagated.
/// </param>
/// <param name="AllowPayloadReferences">
/// Allows reference payloads at a cross-app input boundary instead of requiring inline data.
/// Set only when the destination supports the reference format and can access the referenced store.
/// Does not force offloading or override a preference to inline. Applies only to the top-level input,
/// not results or propagated history.
/// </param>
public record WorkflowTaskOptions(
    WorkflowRetryPolicy? RetryPolicy = null,
    string? TargetAppId = null,
    HistoryPropagationScope PropagationScope = HistoryPropagationScope.None,
    bool AllowPayloadReferences = false)
{
    /// <summary>
    /// Initializes options without cross-app reference permission.
    /// </summary>
    /// <param name="RetryPolicy">The retry policy.</param>
    /// <param name="TargetAppId">The destination app ID.</param>
    /// <param name="PropagationScope">The history propagation scope.</param>
    public WorkflowTaskOptions(WorkflowRetryPolicy? RetryPolicy, string? TargetAppId,
        HistoryPropagationScope PropagationScope)
        : this(RetryPolicy, TargetAppId, PropagationScope, false)
    {
    }

    /// <summary>
    /// Deconstructs options without the cross-app reference permission.
    /// </summary>
    /// <param name="RetryPolicy">The retry policy.</param>
    /// <param name="TargetAppId">The destination app ID.</param>
    /// <param name="PropagationScope">The history propagation scope.</param>
    public void Deconstruct(out WorkflowRetryPolicy? RetryPolicy, out string? TargetAppId,
        out HistoryPropagationScope PropagationScope)
    {
        RetryPolicy = this.RetryPolicy;
        TargetAppId = this.TargetAppId;
        PropagationScope = this.PropagationScope;
    }

    /// <summary>
    /// Returns a new <see cref="WorkflowTaskOptions"/> with the specified history propagation scope.
    /// </summary>
    /// <param name="scope">The propagation scope to apply.</param>
    /// <returns>A new options instance with the propagation scope set.</returns>
    public WorkflowTaskOptions WithHistoryPropagation(HistoryPropagationScope scope) =>
        this with { PropagationScope = scope };
}

/// <summary>
/// Options for controlling the behavior of child workflow execution.
/// </summary>
/// <param name="InstanceId">The instance ID to use for the child workflow.</param>
/// <param name="RetryPolicy">The child workflow's retry policy.</param>
/// <param name="TargetAppId">The App ID indicating the app in which to find the named child workflow to run.</param>
/// <param name="PropagationScope">
/// Determines which ancestor history events are propagated to the child workflow.
/// Defaults to <see cref="HistoryPropagationScope.None"/>, meaning no history is propagated.
/// </param>
/// <param name="AllowPayloadReferences">
/// Allows references in a cross-app child input. The destination must support the reference format
/// and have access to the stored payload. Does not affect results or propagated history.
/// </param>
public record ChildWorkflowTaskOptions(
    string? InstanceId = null,
    WorkflowRetryPolicy? RetryPolicy = null,
    string? TargetAppId = null,
    HistoryPropagationScope PropagationScope = HistoryPropagationScope.None,
    bool AllowPayloadReferences = false)
    : WorkflowTaskOptions(RetryPolicy, TargetAppId, PropagationScope, AllowPayloadReferences)
{
    /// <summary>
    /// Initializes child options without cross-app reference permission.
    /// </summary>
    /// <param name="InstanceId">The child instance ID.</param>
    /// <param name="RetryPolicy">The retry policy.</param>
    /// <param name="TargetAppId">The destination app ID.</param>
    /// <param name="PropagationScope">The history propagation scope.</param>
    public ChildWorkflowTaskOptions(string? InstanceId, WorkflowRetryPolicy? RetryPolicy, string? TargetAppId,
        HistoryPropagationScope PropagationScope)
        : this(InstanceId, RetryPolicy, TargetAppId, PropagationScope, false)
    {
    }

    /// <summary>
    /// Deconstructs child options without the cross-app reference permission.
    /// </summary>
    /// <param name="InstanceId">The child instance ID.</param>
    /// <param name="RetryPolicy">The retry policy.</param>
    /// <param name="TargetAppId">The destination app ID.</param>
    /// <param name="PropagationScope">The history propagation scope.</param>
    public void Deconstruct(out string? InstanceId, out WorkflowRetryPolicy? RetryPolicy, out string? TargetAppId,
        out HistoryPropagationScope PropagationScope)
    {
        InstanceId = this.InstanceId;
        RetryPolicy = this.RetryPolicy;
        TargetAppId = this.TargetAppId;
        PropagationScope = this.PropagationScope;
    }

    /// <summary>
    /// Returns a new <see cref="ChildWorkflowTaskOptions"/> with the specified history propagation scope.
    /// </summary>
    /// <param name="scope">The propagation scope to apply.</param>
    /// <returns>A new options instance with the propagation scope set.</returns>
    /// <remarks>
    /// Hides the base method (records cannot override) so the derived type is returned —
    /// callers must hold a <see cref="ChildWorkflowTaskOptions"/> reference to preserve
    /// <see cref="InstanceId"/>. Calling through a <see cref="WorkflowTaskOptions"/> reference
    /// invokes the base method and returns a plain <see cref="WorkflowTaskOptions"/>.
    /// </remarks>
    public new ChildWorkflowTaskOptions WithHistoryPropagation(HistoryPropagationScope scope) =>
        this with { PropagationScope = scope };
}
