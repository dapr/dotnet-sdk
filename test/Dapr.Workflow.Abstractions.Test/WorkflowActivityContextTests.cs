namespace Dapr.Workflow.Abstractions.Test;

public class WorkflowActivityContextTests
{
    private sealed class TestContext : WorkflowActivityContext
    {
        private readonly TaskIdentifier _id;
        private readonly string _instanceId;

        public TestContext(TaskIdentifier id, string instanceId)
        {
            _id = id;
            _instanceId = instanceId;
        }

        public override TaskIdentifier Identifier => _id;
        public override string InstanceId => _instanceId;
    }

    [Fact]
    public void Properties_Return_Constructor_Values()
    {
        var ctx = new TestContext(new TaskIdentifier("act-1"), "wf-123");
        Assert.Equal("act-1", ctx.Identifier.Name);
        Assert.Equal("wf-123", ctx.InstanceId);
    }
}
