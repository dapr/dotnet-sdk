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

using Dapr.Workflow.Abstractions;

namespace Dapr.Workflow.Worker;

/// <summary>
/// Factory for registering and creating workflow and activity instances.
/// </summary>
public interface IWorkflowsFactory
{
    /// <summary>
    /// Registers a workflow type.
    /// </summary>
    void RegisterWorkflow<TWorkflow>(string? name = null) where TWorkflow : class, IWorkflow;

    /// <summary>
    /// Registers a workflow as a function.
    /// </summary>
    void RegisterWorkflow<TInput, TOutput>(string name,
        Func<WorkflowContext, TInput, Task<TOutput>> implementation);

    /// <summary>
    /// Registers a workflow activity type.
    /// </summary>
    void RegisterActivity<TActivity>(string? name = null) where TActivity : class, IWorkflowActivity;

    /// <summary>
    /// Registers an activity as a function.
    /// </summary>
    void RegisterActivity<TInput, TOutput>(string name,
        Func<WorkflowActivityContext, TInput, Task<TOutput>> implementation);

    /// <summary>
    /// Tries to create a workflow instance.
    /// </summary>
    bool TryCreateWorkflow(TaskIdentifier identifier, IServiceProvider serviceProvider,
        out IWorkflow? workflow, out Exception? activationException);

    /// <summary>
    /// Tries to create an activity instance.
    /// </summary>
    bool TryCreateActivity(TaskIdentifier identifier, IServiceProvider serviceProvider,
        out IWorkflowActivity? activity, out Exception? activationException);
}
