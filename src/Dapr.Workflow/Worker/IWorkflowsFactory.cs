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

using System;
using System.Threading.Tasks;
using Dapr.Workflow.Abstractions;

namespace Dapr.Workflow.Worker;

internal interface IWorkflowsFactory
{
    /// <summary>
    /// Registers a workflow type.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow type to register.</typeparam>
    /// <param name="name">Optional workflow name. If not specified, uses the type name.</param>
    void RegisterWorkflow<TWorkflow>(string? name = null) where TWorkflow : class, IWorkflow;

    /// <summary>
    /// Registers a workflow as a function.
    /// </summary>
    /// <typeparam name="TInput">The type of the workflow input.</typeparam>
    /// <typeparam name="TOutput">The type of the workflow output.</typeparam>
    /// <param name="name">Workflow name.</param>
    /// <param name="implementation">Function implementing the workflow definition.</param>
    void RegisterWorkflow<TInput, TOutput>(string name,
        Func<WorkflowContext, TInput, Task<TOutput>> implementation);

    /// <summary>
    /// Registers a workflow activity type.
    /// </summary>
    /// <typeparam name="TActivity">The activity type to register.</typeparam>
    /// <param name="name">Optional activity name. If not specified, uses the type name.</param>
    void RegisterActivity<TActivity>(string? name = null) where TActivity : class, IWorkflowActivity;

    /// <summary>
    /// Registers an activity as a function. 
    /// </summary>
    /// <param name="name">The name of the activity.</param>
    /// <param name="implementation">The implementation of the activity.</param>
    /// <typeparam name="TInput">The type of the input to the activity.</typeparam>
    /// <typeparam name="TOutput">The type of the output returned from the activity.</typeparam>
    public void RegisterActivity<TInput, TOutput>(string name,
        Func<WorkflowActivityContext, TInput, Task<TOutput>> implementation);
    
    /// <summary>
    /// Tries to create a workflow instance.
    /// </summary>
    /// <param name="identifier">The identifier of the workflow.</param>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    /// <param name="workflow">The created workflow, or null if not found.</param>
    /// <returns>True if the workflow was created; otherwise false.</returns>
    bool TryCreateWorkflow(TaskIdentifier identifier, IServiceProvider serviceProvider, out IWorkflow? workflow);

    /// <summary>
    /// Tries to create an activity instance.
    /// </summary>
    /// <param name="identifier">The identifier of the activity.</param>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    /// <param name="activity">The created activity, or null if not found.</param>
    /// <returns>True if the activity was created; otherwise false.</returns>
    bool TryCreateActivity(TaskIdentifier identifier, IServiceProvider serviceProvider,
        out IWorkflowActivity? activity);
}
