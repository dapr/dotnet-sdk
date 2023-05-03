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
        /// Initializes a new <see cref="GetWorkflowResponse" />.
        /// </summary>
        /// <param name="instanceId">The instance ID associated with this response.</param>
        /// <param name="workflowName">The name of the workflow associated with this response.</param>
        /// <param name="createdAt">The time at which the workflow started executing.</param>
        /// <param name="lastUpdatedAt">The time at which the workflow started executing.</param>
        /// <param name="runtimeStatus">The current runtime status of the workflow.</param>
        /// <param name="properties">The response properties.</param>
        public record GetWorkflowResponse(
                string instanceId,
                string workflowName,
                DateTime createdAt,
                DateTime lastUpdatedAt,
                string runtimeStatus,
                IReadOnlyDictionary<string, string> properties);
}
