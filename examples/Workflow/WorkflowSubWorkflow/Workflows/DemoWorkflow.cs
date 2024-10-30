using Dapr.Workflow;

namespace WorkflowSubWorkflow.Workflows;

internal sealed class DemoWorkflow : Workflow<string, bool>
{
    /// <summary>
    /// Override to implement workflow logic.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <param name="instanceId">The deserialized workflow input.</param>
    /// <returns>The output of the workflow as a task.</returns>
    public override async Task<bool> RunAsync(WorkflowContext context, string instanceId)
    {
        Console.WriteLine($"Workflow {instanceId} started");
        var subInstanceId = instanceId + "-sub";
        var options = new ChildWorkflowTaskOptions(subInstanceId);
        await context.CallChildWorkflowAsync<bool>(nameof(DemoSubWorkflow), "Hello, sub-workflow", options);
        return true;
    }
}
