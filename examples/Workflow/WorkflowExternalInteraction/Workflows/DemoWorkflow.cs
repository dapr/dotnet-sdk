using Dapr.Workflow;
using WorkflowExternalInteraction.Activities;

namespace WorkflowExternalInteraction.Workflows;

internal sealed class DemoWorkflow : Workflow<string, bool>
{
    /// <summary>
    /// Override to implement workflow logic.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <param name="input">The deserialized workflow input.</param>
    /// <returns>The output of the workflow as a task.</returns>
    public override async Task<bool> RunAsync(WorkflowContext context, string input)
    {
        try
        {
            await context.WaitForExternalEventAsync<bool>(eventName: "Approval", timeout: TimeSpan.FromSeconds(10));
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Approval timeout");
            await context.CallActivityAsync(nameof(RejectActivity), input);
            Console.WriteLine("Reject Activity finished");
            return false;
        }

        await context.CallActivityAsync(nameof(ApproveActivity), input);
        Console.WriteLine("Approve Activity finished");

        return true;
    }
}
