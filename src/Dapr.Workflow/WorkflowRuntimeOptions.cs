// ------------------------------------------------------------------------
// Copyright 2022 The Dapr Authors
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

using Grpc.Net.Client;

namespace Dapr.Workflow;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.DurableTask;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Defines runtime options for workflows.
/// </summary>
public sealed class WorkflowRuntimeOptions
{
    /// <summary>
    /// Dictionary to name and register a workflow.
    /// </summary>
    readonly Dictionary<string, Action<DurableTaskRegistry>> factories = new();

    /// <summary>
    /// Override GrpcChannelOptions.
    /// </summary>
    internal GrpcChannelOptions? GrpcChannelOptions { get; private set; }
        
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowRuntimeOptions"/> class.
    /// </summary>
    /// <remarks>
    /// Instances of this type are expected to be instantiated from a dependency injection container.
    /// </remarks>
    public WorkflowRuntimeOptions()
    {
    }

    /// <summary>
    /// Registers a workflow as a function that takes a specified input type and returns a specified output type.
    /// </summary>
    /// <param name="name">Workflow name</param>
    /// <param name="implementation">Function implementing the workflow definition</param>
    public void RegisterWorkflow<TInput, TOutput>(string name, Func<WorkflowContext, TInput, Task<TOutput>> implementation)
    {
        // Dapr workflows are implemented as specialized Durable Task orchestrations
        this.factories.Add(name, (DurableTaskRegistry registry) =>
        {
            registry.AddOrchestratorFunc<TInput, TOutput>(name, (innerContext, input) =>
            {
                WorkflowContext workflowContext = new DaprWorkflowContext(innerContext);
                return implementation(workflowContext, input);
            });
            WorkflowLoggingService.LogWorkflowName(name);
        });
    }

    /// <summary>
    /// Registers a workflow class that derives from <see cref="Workflow{TInput, TOutput}"/>.
    /// </summary>
    /// <typeparam name="TWorkflow">The <see cref="Workflow{TInput, TOutput}"/> type to register.</typeparam>
    public void RegisterWorkflow<TWorkflow>() where TWorkflow : class, IWorkflow, new()
    {
        string name = typeof(TWorkflow).Name;

        // Dapr workflows are implemented as specialized Durable Task orchestrations
        this.factories.Add(name, (DurableTaskRegistry registry) =>
        {
            registry.AddOrchestrator(name, () =>
            {
                TWorkflow workflow = Activator.CreateInstance<TWorkflow>();
                return new OrchestratorWrapper(workflow);
            });
            WorkflowLoggingService.LogWorkflowName(name);
        });
    }

    /// <summary>
    /// Registers a workflow activity as a function that takes a specified input type and returns a specified output type.
    /// </summary>
    /// <param name="name">Activity name</param>
    /// <param name="implementation">Activity implemetation</param>
    public void RegisterActivity<TInput, TOutput>(string name, Func<WorkflowActivityContext, TInput, Task<TOutput>> implementation)
    {
        // Dapr activities are implemented as specialized Durable Task activities
        this.factories.Add(name, (DurableTaskRegistry registry) =>
        {
            registry.AddActivityFunc<TInput, TOutput>(name, (innerContext, input) =>
            {
                WorkflowActivityContext activityContext = new DaprWorkflowActivityContext(innerContext);
                return implementation(activityContext, input);
            });
            WorkflowLoggingService.LogActivityName(name);
        });
    }

    /// <summary>
    /// Registers a workflow activity class that derives from <see cref="WorkflowActivity{TInput, TOutput}"/>.
    /// </summary>
    /// <typeparam name="TActivity">The <see cref="WorkflowActivity{TInput, TOutput}"/> type to register.</typeparam>
    public void RegisterActivity<TActivity>() where TActivity : class, IWorkflowActivity
    {
        string name = typeof(TActivity).Name;

        // Dapr workflows are implemented as specialized Durable Task orchestrations
        this.factories.Add(name, (DurableTaskRegistry registry) =>
        {
            registry.AddActivity(name, serviceProvider =>
            {
                // Workflow activity classes support dependency injection.
                TActivity activity = ActivatorUtilities.CreateInstance<TActivity>(serviceProvider);
                return new ActivityWrapper(activity);
            });
            WorkflowLoggingService.LogActivityName(name);
        });
    }
        
    /// <summary>
    /// Uses the provided <paramref name="grpcChannelOptions" /> for creating the <see cref="GrpcChannel" />.
    /// </summary>
    /// <param name="grpcChannelOptions">The <see cref="GrpcChannelOptions" /> to use for creating the <see cref="GrpcChannel" />.</param>
    public void UseGrpcChannelOptions(GrpcChannelOptions grpcChannelOptions)
    {
        this.GrpcChannelOptions = grpcChannelOptions;
    }

    /// <summary>
    /// Method to add workflows and activities to the registry.
    /// </summary>
    /// <param name="registry">The registry we will add workflows and activities to</param>
    internal void AddWorkflowsAndActivitiesToRegistry(DurableTaskRegistry registry)
    {
        foreach (Action<DurableTaskRegistry> factory in this.factories.Values)
        {
            factory.Invoke(registry); // This adds workflows to the registry indirectly.
        }
    }

    /// <summary>
    /// Helper class that provides a Durable Task orchestrator wrapper for a workflow.
    /// </summary>
    class OrchestratorWrapper : ITaskOrchestrator
    {
        readonly IWorkflow workflow;

        public OrchestratorWrapper(IWorkflow workflow)
        {
            this.workflow = workflow;
        }

        public Type InputType => this.workflow.InputType;

        public Type OutputType => this.workflow.OutputType;

        public Task<object?> RunAsync(TaskOrchestrationContext context, object? input)
        {
            return this.workflow.RunAsync(new DaprWorkflowContext(context), input);
        }
    }

    class ActivityWrapper : ITaskActivity
    {
        readonly IWorkflowActivity activity;

        public ActivityWrapper(IWorkflowActivity activity)
        {
            this.activity = activity;
        }

        public Type InputType => this.activity.InputType;

        public Type OutputType => this.activity.OutputType;

        public Task<object?> RunAsync(TaskActivityContext context, object? input)
        {
            return this.activity.RunAsync(new DaprWorkflowActivityContext(context), input);
        }
    }
}
