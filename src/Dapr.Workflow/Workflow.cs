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
/// Common interface for workflow implementations.
/// </summary>
/// <remarks>
/// Users should not implement workflows using this interface, directly.
/// Instead, <see cref="Workflow{TInput, TOutput}"/> should be used to implement workflows.
/// </remarks>
public interface IWorkflow
{
    /// <summary>
    /// Gets the type of the input parameter that this workflow accepts.
    /// </summary>
    Type InputType { get; }

    /// <summary>
    /// Gets the type of the return value that this workflow produces.
    /// </summary>
    Type OutputType { get; }

    /// <summary>
    /// Invokes the workflow with the specified context and input.
    /// </summary>
    /// <param name="context">The workflow's context.</param>
    /// <param name="input">The workflow's input.</param>
    /// <returns>Returns the workflow output as the result of a <see cref="Task"/>.</returns>
    Task<object?> RunAsync(WorkflowContext context, object? input);
}

/// <summary>
/// Represents the base class for workflows.
/// </summary>
/// <remarks>
/// <para>
///  Workflows describe how actions are executed and the order in which actions are executed. Workflows
///  don't call into external services or do complex computation directly. Rather, they delegate these tasks to
///  <em>activities</em>, which perform the actual work.
/// </para>
/// <para>
///   Workflows can be scheduled using the Dapr client or by other workflows as child-workflows using the
///   <see cref="WorkflowContext.CallChildWorkflowAsync"/> method.
/// </para>
/// <para>
///   Workflows may be replayed multiple times to rebuild their local state after being reloaded into memory.
///   workflow code must therefore be <em>deterministic</em> to ensure no unexpected side effects from execution
///   replay. To account for this behavior, there are several coding constraints to be aware of:
///   <list type="bullet">
///     <item>
///       A workflow must not generate random numbers or random GUIDs, get the current date, read environment
///       variables, or do anything else that might result in a different value if the code is replayed in the future.
///       Activities and built-in properties and methods on the <see cref="WorkflowContext"/> parameter, like
///       <see cref="WorkflowContext.CurrentUtcDateTime"/> and <see cref="WorkflowContext.NewGuid"/>,
///       can be used to work around these restrictions.
///     </item>
///     <item>
///       Workflow logic must be executed on the workflow thread (the thread that calls <see cref="RunAsync"/>.
///       Creating new threads, scheduling callbacks on worker pool threads, or awaiting non-workflow tasks is forbidden
///       and may result in failures or other unexpected behavior. Blocking the workflow thread may also result in unexpected
///       performance degredation. The use of <c>await</c> should be restricted to workflow tasks - i.e. tasks returned from
///       methods on the <see cref="WorkflowContext"/> parameter object or tasks that wrap these workflow tasks, like
///       <see cref="Task.WhenAll(Task[])"/> and <see cref="Task.WhenAny(Task[])"/>.
///     </item>
///     <item>
///       Avoid infinite loops as they could cause the application to run out of memory. Instead, ensure that loops are
///       bounded or use <see cref="WorkflowContext.ContinueAsNew"/> to restart the workflow with a new input.
///     </item>
///     <item>
///       Avoid logging normally in the workflow code because log messages will be duplicated on each replay.
///       Instead, write log statements when <see cref="WorkflowContext.IsReplaying"/> is <c>false</c>.
///     </item>
///   </list>
/// </para>
/// <para>
///   Workflow code is tightly coupled with its execution history so special care must be taken when making changes
///   to workflow code. For example, adding or removing activity tasks to a workflow's code may cause a
///   mismatch between code and history for in-flight workflows. To avoid potential issues related to workflow
///   versioning, consider applying the following code update strategies:
///   <list type="bullet">
///     <item>
///       Deploy multiple versions of applications side-by-side allowing new code to run independently of old code.
///     </item>
///     <item>
///       Rather than changing existing workflows, create new workflows that implement the modified behavior.
///     </item>
///     <item>
///       Ensure all in-flight workflows are complete before applying code changes to existing workflow code.
///     </item>
///     <item>
///       If possible, only make changes to workflow code that won't impact its history or execution path. For
///       example, renaming variables or adding log statements have no impact on a workflow's execution path and
///       are safe to apply to existing workflows.
///     </item>
///   </list>
/// </para>
/// </remarks>
/// <typeparam name="TInput">The type of the input parameter that this workflow accepts. This type must be JSON-serializable.</typeparam>
/// <typeparam name="TOutput">The type of the return value that this workflow produces. This type must be JSON-serializable.</typeparam>
public abstract class Workflow<TInput, TOutput> : IWorkflow
{
    /// <inheritdoc/>
    Type IWorkflow.InputType => typeof(TInput);

    /// <inheritdoc/>
    Type IWorkflow.OutputType => typeof(TOutput);

    /// <inheritdoc/>
    async Task<object?> IWorkflow.RunAsync(WorkflowContext context, object? input)
    {
        return await this.RunAsync(context, (TInput)input!);
    }

    /// <summary>
    /// Override to implement workflow logic.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <param name="input">The deserialized workflow input.</param>
    /// <returns>The output of the workflow as a task.</returns>
    public abstract Task<TOutput> RunAsync(WorkflowContext context, TInput input);
}