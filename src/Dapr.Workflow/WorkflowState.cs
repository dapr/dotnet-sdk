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

using System;
using Dapr.Client;
using Microsoft.DurableTask.Client;

/// <summary>
/// Represents a snapshot of a workflow instance's current state, including runtime status.
/// </summary>
public class WorkflowState
{
    readonly OrchestrationMetadata? workflowState;
    readonly WorkflowTaskFailureDetails? failureDetails;

    internal WorkflowState(OrchestrationMetadata? orchestrationMetadata)
    {
        // This value will be null if the workflow doesn't exist.
        this.workflowState = orchestrationMetadata;
        if (orchestrationMetadata?.FailureDetails != null)
        {
            this.failureDetails = new WorkflowTaskFailureDetails(orchestrationMetadata.FailureDetails);
        }
    }

    /// <summary>
    /// Gets a value indicating whether the requested workflow instance exists.
    /// </summary>
    public bool Exists => this.workflowState != null;

    /// <summary>
    /// Gets a value indicating whether the requested workflow is in a running state.
    /// </summary>
    public bool IsWorkflowRunning => this.workflowState?.RuntimeStatus == OrchestrationRuntimeStatus.Running;

    /// <summary>
    /// Gets a value indicating whether the requested workflow is in a terminal state.
    /// </summary>
    public bool IsWorkflowCompleted => this.workflowState?.IsCompleted == true;

    /// <summary>
    /// Gets the time at which this workflow instance was created.
    /// </summary>
    public DateTimeOffset CreatedAt => this.workflowState?.CreatedAt ?? default;

    /// <summary>
    /// Gets the time at which this workflow instance last had its state updated.
    /// </summary>
    public DateTimeOffset LastUpdatedAt => this.workflowState?.LastUpdatedAt ?? default;

    /// <summary>
    /// Gets the execution status of the workflow.
    /// </summary>
    public WorkflowRuntimeStatus RuntimeStatus
    {
        get
        {
            if (this.workflowState == null)
            {
                return WorkflowRuntimeStatus.Unknown;
            }

            switch (this.workflowState.RuntimeStatus)
            {
                case OrchestrationRuntimeStatus.Running:
                    return WorkflowRuntimeStatus.Running;
                case OrchestrationRuntimeStatus.Completed:
                    return WorkflowRuntimeStatus.Completed;
                case OrchestrationRuntimeStatus.Failed:
                    return WorkflowRuntimeStatus.Failed;
                case OrchestrationRuntimeStatus.Terminated:
                    return WorkflowRuntimeStatus.Terminated;
                case OrchestrationRuntimeStatus.Pending:
                    return WorkflowRuntimeStatus.Pending;
                case OrchestrationRuntimeStatus.Suspended:
                    return WorkflowRuntimeStatus.Suspended;
                default:
                    return WorkflowRuntimeStatus.Unknown;
            }
        }
    }

    /// <summary>
    /// Gets the failure details, if any, for the workflow instance.
    /// </summary>
    /// <remarks>
    /// This property contains data only if the workflow is in the <see cref="WorkflowRuntimeStatus.Failed"/>
    /// state, and only if this instance metadata was fetched with the option to include output data.
    /// </remarks>
    /// <value>The failure details if the workflow was in a failed state; <c>null</c> otherwise.</value>
    public WorkflowTaskFailureDetails? FailureDetails => this.failureDetails;

    /// <summary>
    /// Deserializes the workflow input into <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the workflow input into.</typeparam>
    /// <returns>Returns the input as <typeparamref name="T"/>, or returns a default value if the workflow doesn't exist.</returns>
    public T? ReadInputAs<T>()
    {
        if (this.workflowState == null)
        {
            return default;
        }

        if (string.IsNullOrEmpty(this.workflowState.SerializedInput))
        {
            return default;
        }

        return this.workflowState.ReadInputAs<T>();
    }

    /// <summary>
    /// Deserializes the workflow output into <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the workflow output into.</typeparam>
    /// <returns>Returns the output as <typeparamref name="T"/>, or returns a default value if the workflow doesn't exist.</returns>
    public T? ReadOutputAs<T>()
    {
        if (this.workflowState == null)
        {
            return default;
        }

        if (string.IsNullOrEmpty(this.workflowState.SerializedOutput))
        {
            return default;
        }

        return this.workflowState.ReadOutputAs<T>();
    }

    /// <summary>
    /// Deserializes the workflow's custom status into <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the workflow's custom status into.</typeparam>
    /// <returns>Returns the custom status as <typeparamref name="T"/>, or returns a default value if the workflow doesn't exist.</returns>
    public T? ReadCustomStatusAs<T>()
    {
        if (this.workflowState == null)
        {
            return default;
        }

        if (string.IsNullOrEmpty(this.workflowState.SerializedCustomStatus))
        {
            return default;
        }

        return this.workflowState.ReadCustomStatusAs<T>();
    }
}