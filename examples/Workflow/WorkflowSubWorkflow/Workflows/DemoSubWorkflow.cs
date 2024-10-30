using Dapr.Workflow;

namespace WorkflowSubWorkflow.Workflows;

internal sealed class DemoSubWorkflow : Workflow<string, bool>
{
    /// <summary>
    /// Override to implement workflow logic.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <param name="instanceId">The deserialized workflow input.</param>
    /// <returns>The output of the workflow as a task.</returns>
    public override async Task<bool> RunAsync(WorkflowContext context, string instanceId)
    {
        Console.WriteLine($"Workflow {context.InstanceId} started");
        Console.WriteLine($"Received input: {instanceId}");
        await context.CreateTimer(TimeSpan.FromSeconds(5));
        return true;
    }
}
