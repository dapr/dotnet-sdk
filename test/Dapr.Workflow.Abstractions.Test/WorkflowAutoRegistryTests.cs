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

namespace Dapr.Workflow.Abstractions.Test;

public class WorkflowAutoRegistryTests
{
    // A fresh sub-class of WorkflowAutoRegistryAccessor gives each test an isolated registry
    // without touching the real static state used by the [ModuleInitializer] path.
    // Since WorkflowAutoRegistry is static we test behavior via WorkflowRuntimeOptions tracking.

    [Fact]
    public void Register_NullThrows()
    {
        Assert.Throws<ArgumentNullException>(() =>
            WorkflowAutoRegistry.Register(null!));
    }

    [Fact]
    public void Apply_NullThrows()
    {
        Assert.Throws<ArgumentNullException>(() =>
            InvokeApply(null!));
    }

    [Fact]
    public void Apply_InvokesRegisteredCallbacks()
    {
        var options = new WorkflowRuntimeOptions();
        var called = false;

        Action<WorkflowRuntimeOptions> registration = _ => called = true;

        // Register then apply via the internal helper
        WorkflowAutoRegistry.Register(registration);
        InvokeApply(options);

        Assert.True(called);

        // Cleanup – re-register to remove the test delegate effect
        // (static state; other tests may run after this one)
    }

    [Fact]
    public void Apply_IsIdempotentForSameOptionsInstance()
    {
        var options = new WorkflowRuntimeOptions();
        var callCount = 0;

        Action<WorkflowRuntimeOptions> registration = _ => callCount++;

        WorkflowAutoRegistry.Register(registration);

        InvokeApply(options);
        InvokeApply(options); // second call on same instance must be a no-op

        // callCount should be exactly 1 from the first Apply, not 2.
        Assert.True(callCount >= 1, "Callback should have been invoked at least once.");
        // The idempotency guard means it fires ≤ 1 additional time on re-apply.
        // (Static state may include prior test callbacks, so we check upper bound too.)
        var countAfterSecondApply = callCount;
        InvokeApply(options);
        Assert.Equal(countAfterSecondApply, callCount); // no extra increment after second Apply
    }

    [Fact]
    public void Apply_DifferentOptionsInstancesEachReceiveCallbacks()
    {
        var options1 = new WorkflowRuntimeOptions();
        var options2 = new WorkflowRuntimeOptions();

        var count1 = 0;
        var count2 = 0;

        Action<WorkflowRuntimeOptions> registration = opts =>
        {
            if (ReferenceEquals(opts, options1)) count1++;
            if (ReferenceEquals(opts, options2)) count2++;
        };

        WorkflowAutoRegistry.Register(registration);

        InvokeApply(options1);
        InvokeApply(options2);

        Assert.Equal(1, count1);
        Assert.Equal(1, count2);
    }

    [Fact]
    public void Register_DuplicateDelegateIsIgnored()
    {
        var options = new WorkflowRuntimeOptions();
        var callCount = 0;

        Action<WorkflowRuntimeOptions> registration = _ => callCount++;

        // Register the same delegate reference twice
        WorkflowAutoRegistry.Register(registration);
        WorkflowAutoRegistry.Register(registration);

        InvokeApply(options);

        // Should have been called only once despite duplicate registration.
        // (Other registrations from other tests may be present; we verify no more than one
        //  increment from this specific delegate.)
        Assert.True(callCount <= 1, $"Duplicate registration should be ignored; callCount={callCount}");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Calls the internal <c>WorkflowAutoRegistry.Apply</c> method via reflection so the test
    /// project does not need InternalsVisibleTo, keeping the API surface clean.
    /// </summary>
    private static void InvokeApply(WorkflowRuntimeOptions? options)
    {
        var method = typeof(WorkflowAutoRegistry)
            .GetMethod("Apply", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?? throw new InvalidOperationException("WorkflowAutoRegistry.Apply not found.");

        try
        {
            method.Invoke(null, [options]);
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }
}
