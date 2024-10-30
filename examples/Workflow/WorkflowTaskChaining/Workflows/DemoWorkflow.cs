// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

using Dapr.Workflow;
using WorkflowTaskChaining.Activities;

namespace WorkflowTaskChaining.Workflows;

internal sealed class DemoWorkflow : Workflow<int, int[]>
{
    /// <summary>
    /// Override to implement workflow logic.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <param name="input">The deserialized workflow input.</param>
    /// <returns>The output of the workflow as a task.</returns>
    public override async Task<int[]> RunAsync(WorkflowContext context, int input)
    {
        var result1 = await context.CallActivityAsync<int>(nameof(Step1), input);
        var result2 = await context.CallActivityAsync<int>(nameof(Step2), result1);
        var result3 = await context.CallActivityAsync<int>(nameof(Step3), result2);
        var ret = new int[] { result1, result2, result3 };

        return ret;
    }
}
