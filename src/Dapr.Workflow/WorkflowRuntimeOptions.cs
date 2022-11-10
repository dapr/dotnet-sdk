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

namespace Dapr.Workflow
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using Microsoft.DurableTask;
    using Dapr.Workflow;
    
    /// <summary>
    /// Defines runtime options for workflows.
    /// </summary>
    public sealed class WorkflowRuntimeOptions
    {
        /// <summary>
        /// Dictionary to and name and registery of a workflow.
        /// </summary>
        public Dictionary<string, Action<IDurableTaskRegistry>> factories = new();

        /// <summary>
        /// Registers a workflow as a function that takes a specified input type and returns a specified output type.
        /// </summary>
        public void RegisterWorkflow<TInput, TOutput>(string name, Func<WorkflowContext, TInput?, Task<TOutput?>> implementation)
        {
            // Dapr workflows are implemented as specialized Durable Task orchestrations
            this.factories.Add(name, (IDurableTaskRegistry registry) =>
            {
                registry.AddOrchestrator<TInput, TOutput>(name, (innerContext, input) =>
                {
                    WorkflowContext workflowContext = new(innerContext);
                    return implementation(workflowContext, input);
                });
            });
        }

        /// <summary>
        /// Registers a workflow activity as a function that takes a specified input type and returns a specified output type.
        /// </summary>
        public void RegisterActivity<TInput, TOutput>(string name, Func<ActivityContext, TInput?, Task<TOutput?>> implementation)
        {
            // Dapr activities are implemented as specialized Durable Task activities
            this.factories.Add(name, (IDurableTaskRegistry registry) =>
            {
                registry.AddActivity<TInput, TOutput>(name, (innerContext, input) =>
                {
                    ActivityContext activityContext = new(innerContext);
                    return implementation(activityContext, input);
                });
            });
        }


        /// <summary>
        /// Method to add workflow to the registry.
        /// </summary>
        internal void AddWorkflowsToRegistry(IDurableTaskRegistry registry)
        {
            foreach (Action<IDurableTaskRegistry> factory in this.factories.Values)
            {
                factory.Invoke(registry);
            }
        }
    }
}

