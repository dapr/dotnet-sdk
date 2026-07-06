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
using WorkflowRetryPolicyExample.Activities;

namespace WorkflowRetryPolicyExample.Workflows;

/// <summary>
/// Demonstrates configuring retry policies on activity and child workflow calls.
/// </summary>
internal sealed class RetryPolicyDemo : Workflow<string, string[]>
{
    public override async Task<string[]> RunAsync(WorkflowContext context, string input)
    {
        var results = new List<string>();

        // ---------------------------------------------------------------
        // 1. Activity with a simple retry policy (fixed interval).
        //    The activity will fail twice, then succeed on the 3rd attempt.
        //    We allow up to 5 retries with a 1-second fixed interval.
        // ---------------------------------------------------------------
        var simpleRetryOptions = new WorkflowTaskOptions(
            new WorkflowRetryPolicy(
                maxNumberOfAttempts: 5,
                firstRetryInterval: TimeSpan.FromSeconds(1)));

        var result1 = await context.CallActivityAsync<string>(
            nameof(FlakyActivity),
            new FlakyActivityInput("SimpleRetry", FailUntilAttempt: 3),
            simpleRetryOptions);

        results.Add(result1);
        Console.WriteLine($"Result: {result1}");

        // ---------------------------------------------------------------
        // 2. Activity with an exponential back-off retry policy.
        //    The activity will fail 3 times, then succeed on the 4th attempt.
        //    Uses exponential backoff (coefficient = 2) with a max interval.
        // ---------------------------------------------------------------
        var backoffRetryOptions = new WorkflowTaskOptions(
            new WorkflowRetryPolicy(
                maxNumberOfAttempts: 10,
                firstRetryInterval: TimeSpan.FromSeconds(1),
                backoffCoefficient: 2.0,
                maxRetryInterval: TimeSpan.FromSeconds(16)));

        var result2 = await context.CallActivityAsync<string>(
            nameof(FlakyActivity),
            new FlakyActivityInput("ExponentialBackoff", FailUntilAttempt: 4),
            backoffRetryOptions);

        results.Add(result2);
        Console.WriteLine($"Result: {result2}");

        // ---------------------------------------------------------------
        // 3. Child workflow with a retry policy.
        //    The child workflow itself will throw on the first invocation,
        //    but the retry policy will re-invoke it. This shows that retry
        //    policies also work for sub-workflows.
        // ---------------------------------------------------------------
        var childRetryOptions = new ChildWorkflowTaskOptions(
            RetryPolicy: new WorkflowRetryPolicy(
                maxNumberOfAttempts: 3,
                firstRetryInterval: TimeSpan.FromSeconds(1),
                backoffCoefficient: 1.5,
                maxRetryInterval: TimeSpan.FromSeconds(10)));

        var result3 = await context.CallChildWorkflowAsync<string>(
            nameof(FlakyChildWorkflow),
            input,
            childRetryOptions);

        results.Add(result3);
        Console.WriteLine($"Result: {result3}");

        return results.ToArray();
    }
}
