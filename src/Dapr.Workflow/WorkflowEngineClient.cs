// ------------------------------------------------------------------------
// Copyright 2023 The Dapr Authors
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
    using System.Threading.Tasks;
    using Microsoft.DurableTask;
    using Microsoft.DurableTask.Client;

    /// <summary>
    /// Defines client operations for managing Dapr Workflow instances.
    /// </summary>
    /// <remarks>
    /// This is an alternative to the general purpose Dapr client. It uses a gRPC connection to send
    /// commands directly to the workflow engine, bypassing the Dapr API layer.
    /// </remarks>
    public sealed class WorkflowEngineClient : IAsyncDisposable
    {
        readonly DurableTaskClient innerClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowEngineClient"/> class.
        /// </summary>
        /// <param name="innerClient">The Durable Task client used to communicate with the Dapr sidecar.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="innerClient"/> is <c>null</c>.</exception>
        public WorkflowEngineClient(DurableTaskClient innerClient)
        {
            this.innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
        }

        /// <summary>
        /// Schedules a new workflow instance for execution.
        /// </summary>
        /// <param name="name">The name of the orchestrator to schedule.</param>
        /// <param name="instanceId">
        /// The unique ID of the workflow instance to schedule. If not specified, a new GUID value is used.
        /// </param>
        /// <param name="startTime">
        /// The time when the workflow instance should start executing. If not specified or if a date-time in the past
        /// is specified, the workflow instance will be scheduled immediately.
        /// </param>
        /// <param name="input">
        /// The optional input to pass to the scheduled workflow instance. This must be a serializable value.
        /// </param>
        public Task<string> ScheduleNewWorkflowAsync(
            string name,
            string? instanceId = null,
            object? input = null,
            DateTime? startTime = null)
        {
            StartOrchestrationOptions options = new(instanceId, startTime);
            return this.innerClient.ScheduleNewOrchestrationInstanceAsync(name, input, options);
        }

        /// <summary>
        /// Fetches runtime state for the specified workflow instance.
        /// </summary>
        /// <param name="instanceId">The unique ID of the workflow instance to fetch.</param>
        /// <param name="getInputsAndOutputs">
        /// Specify <c>true</c> to fetch the workflow instance's inputs, outputs, and custom status, or <c>false</c> to
        /// omit them. Defaults to false.
        /// </param>
        public async Task<WorkflowState> GetWorkflowStateAsync(string instanceId, bool getInputsAndOutputs = false)
        {
            OrchestrationMetadata? metadata = await this.innerClient.GetInstancesAsync(
                instanceId,
                getInputsAndOutputs);
            return new WorkflowState(metadata);
        }

        /// <summary>
        /// Disposes any unmanaged resources associated with this client.
        /// </summary>
        public ValueTask DisposeAsync()
        {
            return ((IAsyncDisposable)this.innerClient).DisposeAsync();
        }
    }
}
