using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.DurableTask;
using Dapr.Workflow;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Defines runtime options for workflows.
    /// </summary>
    public sealed class WorkflowRuntimeOptions
    {
        readonly Dictionary<string, Action<IDurableTaskRegistry>> factories = new();

        /// <summary>
        /// Method regitering the workflow.
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
        /// Method regitering the activity..
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

