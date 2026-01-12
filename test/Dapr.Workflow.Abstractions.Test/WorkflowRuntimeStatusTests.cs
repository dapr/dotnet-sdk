namespace Dapr.Workflow.Abstractions.Test;

public class WorkflowRuntimeStatusTests
{
    [Fact]
    public void Enum_Has_Expected_Values()
    {
        Assert.Equal(-1, (int)WorkflowRuntimeStatus.Unknown);
        Assert.Equal(0, (int)WorkflowRuntimeStatus.Running);
        Assert.Equal(1, (int)WorkflowRuntimeStatus.Completed);
        Assert.Equal(2, (int)WorkflowRuntimeStatus.ContinuedAsNew);
        Assert.Equal(3, (int)WorkflowRuntimeStatus.Failed);
        Assert.Equal(4, (int)WorkflowRuntimeStatus.Canceled);
        Assert.Equal(5, (int)WorkflowRuntimeStatus.Terminated);
        Assert.Equal(6, (int)WorkflowRuntimeStatus.Pending);
        Assert.Equal(7, (int)WorkflowRuntimeStatus.Suspended);
        Assert.Equal(8, (int)WorkflowRuntimeStatus.Stalled);
    }

    [Fact]
    public void Enum_ToString_Returns_Names()
    {
        Assert.Equal("Unknown", WorkflowRuntimeStatus.Unknown.ToString());
        Assert.Equal("Running", WorkflowRuntimeStatus.Running.ToString());
        Assert.Equal("Completed", WorkflowRuntimeStatus.Completed.ToString());
        Assert.Equal("ContinuedAsNew", WorkflowRuntimeStatus.ContinuedAsNew.ToString());
        Assert.Equal("Failed", WorkflowRuntimeStatus.Failed.ToString());
        Assert.Equal("Canceled", WorkflowRuntimeStatus.Canceled.ToString());
        Assert.Equal("Terminated", WorkflowRuntimeStatus.Terminated.ToString());
        Assert.Equal("Pending", WorkflowRuntimeStatus.Pending.ToString());
        Assert.Equal("Suspended", WorkflowRuntimeStatus.Suspended.ToString());
        Assert.Equal("Stalled", WorkflowRuntimeStatus.Stalled.ToString());
    }
}
