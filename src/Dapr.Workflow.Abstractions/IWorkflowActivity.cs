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

namespace Dapr.Workflow.Abstractions;

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
    /// Gets the type of the input parameter that this workflow accepts.
    /// </summary>
    Type InputType { get; }
    
    /// <summary>
    /// Gets the type of the return value this workflow produces.
    /// </summary>
    Type OutputType { get; }

    /// <summary>
    /// Invokes the workflow activity with the specified context and input.
    /// </summary>
    /// <param name="context">The activity's context.</param>
    /// <param name="input">The activity's input.</param>
    /// <returns>Returns the workflow activity output as the result of a <see cref="Task"/>.</returns>
    Task<object?> RunAsync(WorkflowActivityContext context, object? input);
}
