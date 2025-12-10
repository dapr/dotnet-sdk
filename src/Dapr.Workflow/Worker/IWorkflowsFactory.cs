// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//  ------------------------------------------------------------------------

using System;
using Dapr.Workflow.Abstractions;

namespace Dapr.Workflow.Worker;

internal interface IWorkflowsFactory
{
    /// <summary>
    /// Tries to create a workflow instance.
    /// </summary>
    /// <param name="identifier">The identifier of the workflow.</param>
    /// <param name="serviceprovider">The service provider for dependency injection.</param>
    /// <param name="workflow">The created workflow, or null if not found.</param>
    /// <returns>True if the workflow was created; otherwise false.</returns>
    bool TryCreateWorkflow(TaskIdentifier identifier, IServiceProvider serviceprovider, out IWorkflow? workflow);

    /// <summary>
    /// Tries to create an activity instance.
    /// </summary>
    /// <param name="identifier">The identifier of the activity.</param>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    /// <param name="activity">The created activity, or null if not found.</param>
    /// <returns>True if the activity was created; otherwise false.</returns>
    bool TryCreateActivity(TaskIdentifier identifier, IServiceProvider serviceProvider,
        out IWorkflowActivity? activity);
}
