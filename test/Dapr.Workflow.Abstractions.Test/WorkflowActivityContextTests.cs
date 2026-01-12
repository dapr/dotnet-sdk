namespace Dapr.Workflow.Abstractions.Test;

public class WorkflowActivityContextTests
{
    private sealed class TestContext : WorkflowActivityContext
    {
        private readonly TaskIdentifier _id;
        private readonly string _instanceId;
        private readonly string _taskExecutionKey;

        public TestContext(TaskIdentifier id, string instanceId, string taskExecutionKey = "exec-1")
        {
            _id = id;
            _instanceId = instanceId;
            _taskExecutionKey = taskExecutionKey;
        }

        public override TaskIdentifier Identifier => _id;
        public override string InstanceId => _instanceId;
        public override string TaskExecutionKey => _taskExecutionKey;
    }

    [Fact]
    public void Properties_Return_Constructor_Values()
    {
        var ctx = new TestContext(new TaskIdentifier("act-1"), "wf-123", "run-42");
        Assert.Equal("act-1", ctx.Identifier.Name);
        Assert.Equal("wf-123", ctx.InstanceId);
        Assert.Equal("run-42", ctx.TaskExecutionKey);
    }
}
