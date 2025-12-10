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
using Dapr.Workflow.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dapr.Workflow.Worker;

/// <summary>
/// Factory for creating workflow and activity instances with DI support.
/// </summary>
internal sealed class WorkflowsFactory(ILogger<WorkflowsFactory> logger) : IWorkflowsFactory
{
    private readonly ConcurrentDictionary<string, Type> _workflows = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Type> _activities = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a workflow type.
    /// </summary>
    public void RegisterWorkflow<TWorkflow>() where TWorkflow : IWorkflow
    {
        var workflowType = typeof(TWorkflow);
        var name = workflowType.Name;

        if (_workflows.TryAdd(name, workflowType))
        {
            logger.LogRegisterWorkflowSuccess(name);
        }
        else
        {
            logger.LogRegisterWorkflowAlreadyRegistered(name);
        }
    }

    /// <summary>
    /// Registers a workflow activity type.
    /// </summary>
    public void RegisterActivity<TActivity>() where TActivity : IWorkflowActivity
    {
        var activityType = typeof(TActivity);
        var name = activityType.Name;

        if (_activities.TryAdd(name, activityType))
        {
            logger.LogRegisterActivitySuccess(name);
        }
        else
        {
            logger.LogRegisterActivityAlreadyRegistered(name);
        }
    }
    
    /// <inheritdoc />
    public bool TryCreateWorkflow(TaskIdentifier identifier, IServiceProvider serviceprovider, out IWorkflow? workflow)
    {
        if (_workflows.TryGetValue(identifier.Name, out var workflowType))
        {
            try
            {
                workflow = (IWorkflow)ActivatorUtilities.CreateInstance(serviceprovider, workflowType);
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
        if (_activities.TryGetValue(identifier.Name, out var activityType))
        {
            try
            {
                activity = (IWorkflowActivity)ActivatorUtilities.CreateInstance(serviceProvider, activityType);
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
}
