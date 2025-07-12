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
using System.Threading.Tasks;

/// <summary>
/// Common interface for workflow activity implementations.
/// </summary>
/// <remarks>
/// Users should not implement workflow activities using this interface, directly.
/// Instead, <see cref="WorkflowActivity{TInput, TOutput}"/> should be used to implement workflow activities.
/// </remarks>
public interface IWorkflowActivity
{
    /// <summary>
    /// Gets the type of the input parameter that this activity accepts.
    /// </summary>
    Type InputType { get; }

    /// <summary>
    /// Gets the type of the return value that this activity produces.
    /// </summary>
    Type OutputType { get; }

    /// <summary>
    /// Invokes the workflow activity with the specified context and input.
    /// </summary>
    /// <param name="context">The workflow activity's context.</param>
    /// <param name="input">The workflow activity's input.</param>
    /// <returns>Returns the workflow activity output as the result of a <see cref="Task"/>.</returns>
    Task<object?> RunAsync(WorkflowActivityContext context, object? input);
}

/// <summary>
/// Base class for workflow activities.
/// </summary>
/// <remarks>
/// <para>
/// Workflow activities are the basic unit of work in a workflow. Activities are the tasks that are
/// orchestrated in the business process. For example, you might create a workflow to process an order. The tasks
/// may involve checking the inventory, charging the customer, and creating a shipment. Each task would be a separate
/// activity. These activities may be executed serially, in parallel, or some combination of both.
/// </para><para>
/// Unlike workflows, activities aren't restricted in the type of work you can do in them. Activities
/// are frequently used to make network calls or run CPU intensive operations. An activity can also return data back to
/// the workflow. The Dapr workflow engine guarantees that each called activity will be executed
/// <strong>at least once</strong> as part of a workflow's execution.
/// </para><para>
/// Because activities only guarantee at least once execution, it's recommended that activity logic be implemented as
/// idempotent whenever possible.
/// </para><para>
/// Activities are invoked by workflows using one of the <see cref="WorkflowContext.CallActivityAsync"/>
/// method overloads.
/// </para>
/// </remarks>
/// <typeparam name="TInput">The type of the input parameter that this activity accepts.</typeparam>
/// <typeparam name="TOutput">The type of the return value that this activity produces.</typeparam>
public abstract class WorkflowActivity<TInput, TOutput> : IWorkflowActivity
{
    /// <inheritdoc/>
    Type IWorkflowActivity.InputType => typeof(TInput);

    /// <inheritdoc/>
    Type IWorkflowActivity.OutputType => typeof(TOutput);

    /// <inheritdoc/>
    async Task<object?> IWorkflowActivity.RunAsync(WorkflowActivityContext context, object? input)
    {
        return await this.RunAsync(context, (TInput)input!);
    }

    /// <summary>
    /// Override to implement async (non-blocking) workflow activity logic.
    /// </summary>
    /// <param name="context">Provides access to additional context for the current activity execution.</param>
    /// <param name="input">The deserialized activity input.</param>
    /// <returns>The output of the activity as a task.</returns>
    public abstract Task<TOutput> RunAsync(WorkflowActivityContext context, TInput input);
}