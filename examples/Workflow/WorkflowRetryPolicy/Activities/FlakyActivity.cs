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

using System.Collections.Concurrent;
using Dapr.Workflow;

namespace WorkflowRetryPolicyExample.Activities;

/// <summary>
/// A sample activity that simulates transient failures. It fails a configurable number
/// of times before succeeding, which is useful for demonstrating retry policies.
/// </summary>
internal sealed class FlakyActivity : WorkflowActivity<FlakyActivityInput, string>
{
    // Track call counts per workflow instance to simulate transient failures.
    private static readonly ConcurrentDictionary<string, int> callCounts = new();

    public override Task<string> RunAsync(WorkflowActivityContext context, FlakyActivityInput input)
    {
        var key = $"{context.InstanceId}-{input.ActivityName}";
        var count = callCounts.AddOrUpdate(key, 1, (_, existing) => existing + 1);

        Console.WriteLine(
            $"[{input.ActivityName}] Attempt {count} of max {input.FailUntilAttempt} " +
            $"before success (instance: {context.InstanceId})");

        if (count < input.FailUntilAttempt)
        {
            throw new ApplicationException(
                $"Simulated transient failure in '{input.ActivityName}' " +
                $"(attempt {count}, will succeed on attempt {input.FailUntilAttempt}).");
        }

        Console.WriteLine($"[{input.ActivityName}] Succeeded on attempt {count}!");
        return Task.FromResult($"{input.ActivityName} completed after {count} attempt(s)");
    }
}

/// <summary>
/// Input for the <see cref="FlakyActivity"/>. 
/// </summary>
/// <param name="ActivityName">A friendly name for identifying this activity call in logs.</param>
/// <param name="FailUntilAttempt">The attempt number on which the activity will succeed (e.g. 3 means fail twice, succeed on the 3rd attempt).</param>
internal sealed record FlakyActivityInput(string ActivityName, int FailUntilAttempt);
