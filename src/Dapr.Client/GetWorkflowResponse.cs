// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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
using System.Collections.Generic;

namespace Dapr.Client
{
    /// <summary>
    /// Represents the response from invoking a binding.
    /// </summary>
    public sealed class GetWorkflowResponse
    {
        /// <summary>
        /// Initializes a new <see cref="GetWorkflowResponse" />.`
        /// </summary>
        /// <param name="instanceId">The instance ID assocated with this response.</param>
        /// <param name="startTime">The time at which the workflow started executing.</param>
        /// <param name="metadata">The response metadata.</param>
        public GetWorkflowResponse(string instanceId, Int64 startTime, IReadOnlyDictionary<string, string> metadata)
        {
            ArgumentVerifier.ThrowIfNull(instanceId, nameof(instanceId));
            ArgumentVerifier.ThrowIfNull(startTime, nameof(startTime));
            ArgumentVerifier.ThrowIfNull(metadata, nameof(metadata));

            this.InstanceId = instanceId;
            this.StartTime = startTime;
            this.Metadata = metadata;
        }

        /// <summary>
        /// Gets the workflow instance ID assocated with this response.
        /// </summary>
        public string InstanceId { set; get; }

        /// <summary>
        /// Gets the time that the workflow started.
        /// </summary>
        public Int64 StartTime { set; get; }

        /// <summary>
        /// Gets the response metadata from the associated workflow. This includes information such as start time and status of workflow.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { set; get; }
    }
}
