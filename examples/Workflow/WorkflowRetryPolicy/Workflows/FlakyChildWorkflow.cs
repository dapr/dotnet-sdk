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
// ------------------------------------------------------------------------

using Dapr.Workflow;

namespace WorkflowRetryPolicyExample.Workflows;

/// <summary>
/// A child workflow that simulates a transient failure by throwing on the first
/// invocation. When configured with a retry policy, the parent workflow will
/// re-invoke this child workflow on failure.
/// </summary>
internal sealed class FlakyChildWorkflow : Workflow<string, string>
{
    private static int invocationCount;

    public override Task<string> RunAsync(WorkflowContext context, string input)
    {
        var count = Interlocked.Increment(ref invocationCount);

        Console.WriteLine($"[FlakyChildWorkflow] Invocation #{count} (instance: {context.InstanceId})");

        if (count < 2)
        {
            throw new ApplicationException(
                $"Simulated transient failure in child workflow (invocation #{count}).");
        }

        var result = $"FlakyChildWorkflow completed with input '{input}' on invocation #{count}";
        Console.WriteLine($"[FlakyChildWorkflow] {result}");
        return Task.FromResult(result);
    }
}
