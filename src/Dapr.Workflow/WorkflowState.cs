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

using Dapr.Workflow.Client;
using System;

namespace Dapr.Workflow;

/// <summary>
/// Represents a snapshot of a workflow instance's current state, including runtime status.
/// </summary>
public sealed class WorkflowState
{
    private readonly WorkflowMetadata? _metadata;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowState"/> class from workflow metadata.
    /// </summary>
    /// <param name="metadata">The workflow metadata, or <c>null</c> if the workflow does not exist.</param>
    internal WorkflowState(WorkflowMetadata? metadata)
    {
        _metadata = metadata;
    }
    
    /// <summary>
    /// Gets a value indicating whether the requested workflow instance exists.
    /// </summary>
    public bool Exists => _metadata is not null;

    /// <summary>
    /// Gets a value indicating whether the requested workflow is in a running state.
    /// </summary>
    public bool IsWorkflowRunning => _metadata?.RuntimeStatus == WorkflowRuntimeStatus.Running;

    /// <summary>
    /// Gets a value indicating whether the requested workflow is in a terminal state.
    /// </summary>
    public bool IsWorkflowCompleted => _metadata?.RuntimeStatus is
        WorkflowRuntimeStatus.Completed or
        WorkflowRuntimeStatus.Failed or
        WorkflowRuntimeStatus.Terminated;

    /// <summary>
    /// Gets the time at which this workflow instance was created.
    /// </summary>
    public DateTimeOffset CreatedAt => _metadata?.CreatedAt ?? default;

    /// <summary>
    /// Gets the time at which this workflow instance last had its state updated.
    /// </summary>
    public DateTimeOffset LastUpdatedAt => _metadata?.LastUpdatedAt ?? default;

    /// <summary>
    /// Gets the execution status of the workflow.
    /// </summary>
    public WorkflowRuntimeStatus RuntimeStatus => _metadata?.RuntimeStatus ?? WorkflowRuntimeStatus.Unknown;

    /// <summary>
    /// Gets the failure details, if any, for the workflow instance.
    /// </summary>
    /// <remarks>
    /// This property contains data only if the workflow is in the <see cref="WorkflowRuntimeStatus.Failed"/>
    /// state, and only if this instance metadata was fetched with the option to include output data.
    /// </remarks>
    /// <value>The failure details if the workflow was in a failed state; <c>null</c> otherwise.</value>
    public WorkflowTaskFailureDetails? FailureDetails => _metadata?.FailureDetails;

    /// <summary>
    /// Deserializes the workflow input into <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the workflow input into.</typeparam>
    /// <returns>Returns the input as <typeparamref name="T"/>, or returns a default value if the workflow doesn't exist.</returns>
    public T? ReadInputAs<T>() => _metadata is null ? default : _metadata.ReadInputAs<T>();

    /// <summary>
    /// Deserializes the workflow output into <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the workflow output into.</typeparam>
    /// <returns>Returns the output as <typeparamref name="T"/>, or returns a default value if the workflow doesn't exist.</returns>
    public T? ReadOutputAs<T>() => _metadata is null ? default : _metadata.ReadOutputAs<T>();

    /// <summary>
    /// Deserializes the workflow's custom status into <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the workflow's custom status into.</typeparam>
    /// <returns>Returns the custom status as <typeparamref name="T"/>, or returns a default value if the workflow doesn't exist.</returns>
    public T? ReadCustomStatusAs<T>() => _metadata is null ? default : _metadata.ReadCustomStatusAs<T>();
}
