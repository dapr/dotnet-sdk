using Dapr.Workflow;

namespace Dapr.Workflow.Abstractions.Test;

public class WorkflowRetryPolicyTests
{
    [Fact]
    public void Constructor_Validates_Arguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowRetryPolicy(0, TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowRetryPolicy(1, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowRetryPolicy(1, TimeSpan.FromSeconds(1), 0.9));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowRetryPolicy(1, TimeSpan.FromSeconds(5), 1.0, TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowRetryPolicy(1, TimeSpan.FromSeconds(5), 1.0, null, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Constructor_Sets_Properties_And_Defaults()
    {
        var policy = new WorkflowRetryPolicy(5, TimeSpan.FromSeconds(2));
        Assert.Equal(5, policy.MaxNumberOfAttempts);
        Assert.Equal(TimeSpan.FromSeconds(2), policy.FirstRetryInterval);
        Assert.Equal(1.0, policy.BackoffCoefficient);
        // Default max retry interval is 1 hour (stored as null meaning default used in ctor logic)
        Assert.Null(policy.MaxRetryInterval);
        Assert.Equal(System.Threading.Timeout.InfiniteTimeSpan, policy.RetryTimeout);
    }

    [Fact]
    public void GetNextDelay_Computes_With_Backoff_And_Caps_At_Max()
    {
        var policy = new WorkflowRetryPolicy(
            maxNumberOfAttempts: 10,
            firstRetryInterval: TimeSpan.FromSeconds(1),
            backoffCoefficient: 2.0,
            maxRetryInterval: TimeSpan.FromSeconds(5),
            retryTimeout: TimeSpan.FromMinutes(1));

        // attempt 1 => 1s
        Assert.Equal(TimeSpan.FromSeconds(1), InvokeGetNextDelay(policy, 1));
        // attempt 2 => 2s
        Assert.Equal(TimeSpan.FromSeconds(2), InvokeGetNextDelay(policy, 2));
        // attempt 3 => 4s
        Assert.Equal(TimeSpan.FromSeconds(4), InvokeGetNextDelay(policy, 3));
        // attempt 4 => 8s, but capped to 5s
        Assert.Equal(TimeSpan.FromSeconds(5), InvokeGetNextDelay(policy, 4));
        // non-positive attempt returns zero
        Assert.Equal(TimeSpan.Zero, InvokeGetNextDelay(policy, 0));
    }

    private static TimeSpan InvokeGetNextDelay(WorkflowRetryPolicy policy, int attempt)
    {
        var mi = typeof(WorkflowRetryPolicy).GetMethod("GetNextDelay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return (TimeSpan)mi.Invoke(policy, new object[] { attempt })!;
    }
}
