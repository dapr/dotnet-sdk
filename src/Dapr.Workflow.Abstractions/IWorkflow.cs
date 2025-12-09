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

namespace Dapr.Workflow;

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
