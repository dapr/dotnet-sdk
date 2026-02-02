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

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Dapr.Workflow.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dapr.Workflow.Worker;

/// <summary>
/// Factory for creating workflow and activity instances with DI support.
/// </summary>
internal sealed class WorkflowsFactory(ILogger<WorkflowsFactory> logger) : IWorkflowsFactory
{
    private readonly ConcurrentDictionary<string, Func<IServiceProvider, IWorkflow>> _workflowFactories =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Func<IServiceProvider, IWorkflowActivity>> _activityFactories =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void RegisterWorkflow<TWorkflow>(string? name = null) where TWorkflow : class, IWorkflow
    {
        name ??= typeof(TWorkflow).Name;

        if (_workflowFactories.TryAdd(name, sp => ActivatorUtilities.CreateInstance<TWorkflow>(sp)))
        {
            logger.LogRegisterWorkflowSuccess(name);
        }
        else
        {
            logger.LogRegisterWorkflowAlreadyRegistered(name);
        }
    }
    
    /// <inheritdoc />
    public void RegisterWorkflow<TInput, TOutput>(string name,
        Func<WorkflowContext, TInput, Task<TOutput>> implementation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(implementation);

        if (_workflowFactories.TryAdd(name, _ => new FunctionWorkflow<TInput, TOutput>(implementation)))
        {
            logger.LogRegisterWorkflowSuccess(name);
        }
        else
        {
            logger.LogRegisterWorkflowAlreadyRegistered(name);
        }
    }
    
    /// <inheritdoc />
    public void RegisterActivity<TActivity>(string? name = null) where TActivity : class, IWorkflowActivity
    {
        name ??= typeof(TActivity).Name;

        if (_activityFactories.TryAdd(name, sp => ActivatorUtilities.CreateInstance<TActivity>(sp)))
        {
            logger.LogRegisterActivitySuccess(name);
        }
        else
        {
            logger.LogRegisterActivityAlreadyRegistered(name);
        }
    }

    /// <inheritdoc />
    public void RegisterActivity<TInput, TOutput>(string name,
        Func<WorkflowActivityContext, TInput, Task<TOutput>> implementation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(implementation);
        
        // Create a synthetic type that wraps the function
        if (_activityFactories.TryAdd(name, _ => new FunctionActivity<TInput, TOutput>(implementation)))
        {
            logger.LogRegisterActivitySuccess(name);
        }
        else
        {
            logger.LogRegisterActivityAlreadyRegistered(name);
        }
    }
    
    /// <inheritdoc />
    public bool TryCreateWorkflow(TaskIdentifier identifier, IServiceProvider serviceProvider, out IWorkflow? workflow)
    {
        if (_workflowFactories.TryGetValue(identifier.Name, out var factory))
        {
            try
            {
                workflow = factory(serviceProvider);
                logger.LogCreateWorkflowInstanceSuccess(identifier.Name);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogCreateWorkflowFailure(ex, identifier.Name);
                workflow = null;
                return false;
            }
        }
        
        logger.LogCreateWorkflowNotFoundInRegistry(identifier.Name);
        workflow = null;
        return false;
    }

    /// <inheritdoc />
    public bool TryCreateActivity(TaskIdentifier identifier, IServiceProvider serviceProvider, out IWorkflowActivity? activity)
    {
        if (_activityFactories.TryGetValue(identifier.Name, out var factory))
        {
            try
            {
                activity = factory(serviceProvider);
                logger.LogCreateActivityInstanceSuccess(identifier.Name);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogCreateActivityFailure(ex, identifier.Name);
                activity = null;
                return false;
            }
        }
        
        logger.LogCreateActivityNotFoundInRegistry(identifier.Name);
        activity = null;
        return false;
    }

    /// <summary>
    /// Internal wrapper that adapts a function to <see cref="IWorkflow"/>.
    /// </summary>
    private sealed class FunctionWorkflow<TInput, TOutput>(Func<WorkflowContext, TInput, Task<TOutput>> implementation) : IWorkflow
    {
        public Type InputType => typeof(TInput);
        public Type OutputType => typeof(TOutput);

        public async Task<object?> RunAsync(WorkflowContext context, object? input)
        {
            return await implementation(context, (TInput)input!);
        }
    }

    /// <summary>
    /// Internal wrapper that adapts a function to <see cref="IWorkflowActivity"/>.
    /// </summary>
    private sealed class FunctionActivity<TInput, TOutput>(Func<WorkflowActivityContext, TInput, Task<TOutput>> implementation) : IWorkflowActivity
    {
        public Type InputType => typeof(TInput);
        public Type OutputType => typeof(TOutput);

        public async Task<object?> RunAsync(WorkflowActivityContext context, object? input)
        {
            return await implementation(context, (TInput)input!);
        }
    }
}
