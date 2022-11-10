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

using Microsoft.DurableTask;

namespace Dapr.Workflow
{
    /// <summary>
    /// Represents a snapshot of a workflow instance's current state, including metadata.
    /// </summary>
    public class WorkflowMetadata
    {
        internal WorkflowMetadata(OrchestrationMetadata? metadata)
        {
            this.Details = metadata;
        }

        /// <summary>
        /// Gets a value indicating whether the requested workflow instance exists.
        /// </summary>
        public bool Exists => this.Details != null;

        /// <summary>
        /// Gets a value indicating whether the requested workflow is in a running state.
        /// </summary>
        public bool IsWorkflowRunning => this.Details?.RuntimeStatus == OrchestrationRuntimeStatus.Running;

        /// <summary>
        /// Gets the detailed metadata for the requested workflow instance. 
        /// This value will be <c>null</c> when <see cref="Exists" /> is <c>false</c>.
        /// </summary>
        public OrchestrationMetadata? Details { get; }
    }
}