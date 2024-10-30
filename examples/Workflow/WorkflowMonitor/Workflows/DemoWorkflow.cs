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
using WorkflowMonitor.Activities;

namespace WorkflowMonitor.Workflows;

internal sealed class DemoWorkflow : Workflow<bool, bool>
{
    /// <summary>
    /// Override to implement workflow logic.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <param name="isHealthy">The deserialized workflow input.</param>
    /// <returns>The output of the workflow as a task.</returns>
    public override async Task<bool> RunAsync(WorkflowContext context, bool isHealthy)
    {
        string status = await context.CallActivityAsync<string>(nameof(CheckStatus), true);
        int next_sleep_interval;
        if (!context.IsReplaying)
        {
            Console.WriteLine($"This job is {status}");
        }

        if (status == "healthy")
        {
            isHealthy = true;
            next_sleep_interval = 30;
        }
        else
        {
            if (isHealthy)
            {
                isHealthy = false;
            }
            Console.WriteLine("Status is unhealthy. Set check interval to 5s");
            next_sleep_interval = 5;
        }
        
        await context.CreateTimer(TimeSpan.FromSeconds(next_sleep_interval));
        context.ContinueAsNew(isHealthy);
        
        //This workflow will never complete
        return true;
    }
}
