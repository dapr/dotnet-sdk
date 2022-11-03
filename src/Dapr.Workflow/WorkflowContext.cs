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
    using Microsoft.DurableTask;

    /// <summary>
    /// Defines context methods to be called in the workflow definition.
    /// </summary>
    public class WorkflowContext
    {
        readonly TaskOrchestrationContext innerContext;

        internal WorkflowContext(TaskOrchestrationContext innerContext)
        {
            this.innerContext = innerContext ?? throw new ArgumentNullException(nameof(innerContext));
        }

        /// <summary>
        /// Method to get the workflow name.
        /// </summary>
        public TaskName Name => this.innerContext.Name;
        /// <summary>
        /// Method to get the workflow id.
        /// </summary>
        public string InstanceId => this.innerContext.InstanceId;

        /// <summary>
        /// Method to get the current UTC Date time.
        /// </summary>
        public DateTime CurrentUtcDateTime => this.innerContext.CurrentUtcDateTime;

        /// <summary>
        /// Method to set the custom status to the workflow.
        /// </summary>
        public void SetCustomStatus(object? customStatus) => this.innerContext.SetCustomStatus(customStatus);

        /// <summary>
        /// Method to create a timer for the workflow.
        /// </summary>
        public Task CreateTimer(TimeSpan delay, CancellationToken cancellationToken = default)
        {
            return this.innerContext.CreateTimer(delay, cancellationToken);
        }

        /// <summary>
        /// Method to wait for the external event.
        /// </summary>
        public Task<T> WaitForExternalEventAsync<T>(string eventName, TimeSpan timeout)
        {
            return this.innerContext.WaitForExternalEvent<T>(eventName, timeout);
        }

        /// <summary>
        /// Method to call the activity.
        /// </summary>
        public Task<T> CallActivityAsync<T>(TaskName name, object? input = null, TaskOptions? options = null)
        {
            return this.innerContext.CallActivityAsync<T>(name, input, options);
        }
    }
}