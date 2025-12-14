namespace Dapr.Workflow.Abstractions.Test;

public class IWorkflowActivityTests
{
    private sealed class EchoActivity : IWorkflowActivity
    {
        public Type InputType => typeof(string);
        public Type OutputType => typeof(string);

        public Task<object?> RunAsync(WorkflowActivityContext context, object? input)
        {
            // Prefix with instance id and identifier to ensure we used context
            var prefix = $"{context.InstanceId}:{context.Identifier.Name}:";
            return Task.FromResult<object?>(prefix + (input as string));
        }
    }

    private sealed class Ctx(string id, string instance) : WorkflowActivityContext
    {
        public override TaskIdentifier Identifier { get; } = new TaskIdentifier(id);
        public override string InstanceId { get; } = instance;
    }

    [Fact]
    public async Task Activity_Reports_Types_And_Runs()
    {
        var act = new EchoActivity();
        Assert.Equal(typeof(string), act.InputType);
        Assert.Equal(typeof(string), act.OutputType);

        var ctx = new Ctx("echo", "wf-1");
        var result = await act.RunAsync(ctx, "hi");
        Assert.Equal("wf-1:echo:hi", result);
    }
}
