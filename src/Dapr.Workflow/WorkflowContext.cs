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
    /// Context object used by workflow implementations to perform actions such as scheduling activities, durable timers, waiting for
    /// external events, and for getting basic information about the current workflow instance.
    /// </summary>
    public class WorkflowContext
    {
        readonly TaskOrchestrationContext innerContext;

        internal WorkflowContext(TaskOrchestrationContext innerContext)
        {
            this.innerContext = innerContext ?? throw new ArgumentNullException(nameof(innerContext));
        }

        /// <summary>
        /// Gets the name of the current workflow.
        /// </summary>
        public TaskName Name => this.innerContext.Name;

        /// <summary>
        /// Gets the instance ID of the current workflow.
        /// </summary>
        public string InstanceId => this.innerContext.InstanceId;

        /// <summary>
        /// Gets the current workflow time in UTC.
        /// </summary>
        public DateTime CurrentUtcDateTime => this.innerContext.CurrentUtcDateTime;

        /// <summary>
        /// Assigns a custom status value to the current workflow.
        /// </summary>
        public void SetCustomStatus(object? customStatus) => this.innerContext.SetCustomStatus(customStatus);

        /// <summary>
        /// Creates a durable timer that expires after the specified delay.
        /// </summary>
        public Task CreateTimer(TimeSpan delay, CancellationToken cancellationToken = default)
        {
            return this.innerContext.CreateTimer(delay, cancellationToken);
        }

        /// <summary>
        /// Waits for an event to be raised with name <paramref name="eventName"/> and returns the event data.
        /// </summary>
        public Task<T> WaitForExternalEventAsync<T>(string eventName, TimeSpan timeout)
        {
            return this.innerContext.WaitForExternalEvent<T>(eventName, timeout);
        }

        /// <summary>
        /// Asynchronously invokes an activity by name and with the specified input value.
        /// </summary>
        public Task<T> CallActivityAsync<T>(TaskName name, object? input = null, TaskOptions? options = null)
        {
            return this.innerContext.CallActivityAsync<T>(name, input, options);
        }
    }
}