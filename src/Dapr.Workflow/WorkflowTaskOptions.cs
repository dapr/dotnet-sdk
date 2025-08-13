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

using Dapr.DurableTask;

namespace Dapr.Workflow;

/// <summary>
/// Options that can be used to control the behavior of workflow task execution.
/// </summary>
/// <param name="RetryPolicy">The workflow retry policy.</param>
public record WorkflowTaskOptions(WorkflowRetryPolicy? RetryPolicy = null)
{
    internal TaskOptions ToDurableTaskOptions()
    {
        TaskRetryOptions? retryOptions = null;
        if (this.RetryPolicy is not null)
        {
            retryOptions = this.RetryPolicy.GetDurableRetryPolicy();
        }

        return new TaskOptions(retryOptions);
    }
}

/// <summary>
/// Options for controlling the behavior of child workflow execution.
/// </summary>
/// <param name="InstanceId">The instance ID to use for the child workflow.</param>
/// <param name="RetryPolicy">The child workflow's retry policy.</param>
public record ChildWorkflowTaskOptions(string? InstanceId = null, WorkflowRetryPolicy? RetryPolicy = null) : WorkflowTaskOptions(RetryPolicy)
{
    internal new SubOrchestrationOptions ToDurableTaskOptions()
    {
        TaskRetryOptions? retryOptions = null;
        if (this.RetryPolicy is not null)
        {
            retryOptions = this.RetryPolicy.GetDurableRetryPolicy();
        }

        return new SubOrchestrationOptions(retryOptions, this.InstanceId);
    }
}
