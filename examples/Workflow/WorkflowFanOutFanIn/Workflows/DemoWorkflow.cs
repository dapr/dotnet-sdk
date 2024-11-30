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
using WorkflowFanOutFanIn.Activities;

namespace WorkflowFanOutFanIn.Workflows;

public sealed class DemoWorkflow : Workflow<string, string>
{
    /// <summary>
    /// Override to implement workflow logic.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <param name="input">The deserialized workflow input.</param>
    /// <returns>The output of the workflow as a task.</returns>
    public override async Task<string> RunAsync(WorkflowContext context, string input)
    {
        var tasks = new List<Task>();
        for (var a = 1; a <= 3; a++)
        {
            var task = context.CallActivityAsync(nameof(NotifyActivity), $"calling task {a}");
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        return "Workflow completed";
    }
}
