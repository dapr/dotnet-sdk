// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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

using System.Reflection;

namespace Dapr.Workflow.Abstractions.Test;

public sealed class WorkflowRetryPolicyDelayTests
{
    private static readonly MethodInfo GetNextDelayMethod =
        typeof(WorkflowRetryPolicy).GetMethod(
            "GetNextDelay",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

    [Theory]
    [InlineData(1, 2000)]
    [InlineData(2, 4000)]
    [InlineData(3, 5000)]
    [InlineData(4, 5000)]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    public void GetNextDelay_ShouldApplyBackoffAndCap(int attemptNumber, int expectedDelayMs)
    {
        var policy = new WorkflowRetryPolicy(
            maxNumberOfAttempts: 10,
            firstRetryInterval: TimeSpan.FromSeconds(2),
            backoffCoefficient: 2.0,
            maxRetryInterval: TimeSpan.FromSeconds(5));

        var delay = InvokeGetNextDelay(policy, attemptNumber);

        Assert.Equal(TimeSpan.FromMilliseconds(expectedDelayMs), delay);
    }

    [Fact]
    public void GetNextDelay_ShouldNotCap_WhenMaxRetryIntervalIsNull()
    {
        var policy = new WorkflowRetryPolicy(
            maxNumberOfAttempts: 10,
            firstRetryInterval: TimeSpan.FromSeconds(2),
            backoffCoefficient: 2.0);

        var delay = InvokeGetNextDelay(policy, 3);

        Assert.Equal(TimeSpan.FromSeconds(8), delay);
    }

    private static TimeSpan InvokeGetNextDelay(WorkflowRetryPolicy policy, int attemptNumber) =>
        (TimeSpan)GetNextDelayMethod.Invoke(policy, [attemptNumber])!;
}
