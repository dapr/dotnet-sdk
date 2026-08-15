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

using System;

namespace Dapr.Workflow;

/// <summary>
/// Represents a snapshot of a workflow instance's current state, including runtime status.
/// </summary>
/// <remarks>
/// This interface is provided primarily as a testability seam: consumer code that depends on
/// workflow state can be written against <see cref="IWorkflowState"/> and mocked or faked in unit
/// tests without depending on the concrete <see cref="WorkflowState"/> implementation, whose
/// constructor is internal. The <see cref="DaprWorkflowClient"/> continues to return the concrete
/// <see cref="WorkflowState"/>, which implements this interface.
/// </remarks>
public interface IWorkflowState
{
    /// <summary>
    /// Gets the name of the requested workflow that the state corresponds to.
    /// </summary>
    string? WorkflowName { get; }

    /// <summary>
    /// Gets a value indicating whether the requested workflow instance exists.
    /// </summary>
    bool Exists { get; }

    /// <summary>
    /// Gets a value indicating whether the requested workflow is in a running state.
    /// </summary>
    bool IsWorkflowRunning { get; }

    /// <summary>
    /// Gets a value indicating whether the requested workflow is in a terminal state.
    /// </summary>
    bool IsWorkflowCompleted { get; }

    /// <summary>
    /// Gets the time at which this workflow instance was created.
    /// </summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the time at which this workflow instance last had its state updated.
    /// </summary>
    DateTimeOffset LastUpdatedAt { get; }

    /// <summary>
    /// Gets the execution status of the workflow.
    /// </summary>
    WorkflowRuntimeStatus RuntimeStatus { get; }

    /// <summary>
    /// Gets the failure details, if any, for the workflow instance.
    /// </summary>
    /// <remarks>
    /// This property contains data only if the workflow is in the <see cref="WorkflowRuntimeStatus.Failed"/>
    /// state, and only if this instance metadata was fetched with the option to include output data.
    /// </remarks>
    /// <value>The failure details if the workflow was in a failed state; <c>null</c> otherwise.</value>
    WorkflowTaskFailureDetails? FailureDetails { get; }

    /// <summary>
    /// Deserializes the workflow input into <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the workflow input into.</typeparam>
    /// <returns>Returns the input as <typeparamref name="T"/>, or returns a default value if the workflow doesn't exist.</returns>
    T? ReadInputAs<T>();

    /// <summary>
    /// Deserializes the workflow output into <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the workflow output into.</typeparam>
    /// <returns>Returns the output as <typeparamref name="T"/>, or returns a default value if the workflow doesn't exist.</returns>
    T? ReadOutputAs<T>();

    /// <summary>
    /// Deserializes the workflow's custom status into <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the workflow's custom status into.</typeparam>
    /// <returns>Returns the custom status as <typeparamref name="T"/>, or returns a default value if the workflow doesn't exist.</returns>
    T? ReadCustomStatusAs<T>();
}
