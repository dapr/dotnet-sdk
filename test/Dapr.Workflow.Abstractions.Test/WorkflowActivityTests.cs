using Dapr.Workflow;
using Dapr.Workflow.Abstractions;

namespace Dapr.Workflow.Abstractions.Test;

public class WorkflowActivityTests
{
    private sealed class LengthActivity : WorkflowActivity<string, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, string input)
            => Task.FromResult(input?.Length ?? 0);
    }

    private sealed class TestActivityContext : WorkflowActivityContext
    {
        public override TaskIdentifier Identifier => new("len");
        public override string InstanceId => "instance-1";
    }

    [Fact]
    public async Task Generic_Base_Exposes_IWorkflowActivity_Types_And_Runs()
    {
        var activity = new LengthActivity();
        var i = (IWorkflowActivity)activity;

        Assert.Equal(typeof(string), i.InputType);
        Assert.Equal(typeof(int), i.OutputType);

        var ctx = new TestActivityContext();
        var result = await i.RunAsync(ctx, "abc");
        Assert.Equal(3, (int)result!);
    }
}
