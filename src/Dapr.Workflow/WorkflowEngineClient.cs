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
    using Microsoft.DurableTask.Client;

    /// <summary>
    /// Deprecated. Use <see cref="DaprWorkflowClient"/> instead.
    /// </summary>
    [Obsolete($"Deprecated. Use {nameof(DaprWorkflowClient)} instead.")]
    public sealed class WorkflowEngineClient : DaprWorkflowClient
    {
        /// <summary>
        /// Deprecated. Use <see cref="DaprWorkflowClient"/> instead.
        /// </summary>
        /// <inheritdoc cref="DaprWorkflowClient(DurableTaskClient)"/>
        public WorkflowEngineClient(DurableTaskClient innerClient)
            : base(innerClient)
        {
        }
    }
}
