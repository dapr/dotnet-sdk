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
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.DurableTask;

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
                    WorkflowContext workflowContext = new(innerContext);
                    return implementation(workflowContext, input);
                });
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
                    WorkflowActivityContext activityContext = new(innerContext);
                    return implementation(activityContext, input);
                });
            });
        }

        /// <summary>
        /// Method to add activities to the registry.
        /// </summary>
        /// <param name="registry">The registry we will add activities to</param>
        internal void AddActivitiesToRegistry(DurableTaskRegistry registry)
        {
            foreach (Action<DurableTaskRegistry> factory in this.factories.Values)
            {
                factory.Invoke(registry);
            }
        }
    }
}

