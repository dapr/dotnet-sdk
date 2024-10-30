using Dapr.Workflow;

namespace WorkflowExternalInteraction.Activities;

internal sealed class RejectActivity : WorkflowActivity<string, bool>
{
    /// <summary>
    /// Override to implement async (non-blocking) workflow activity logic.
    /// </summary>
    /// <param name="context">Provides access to additional context for the current activity execution.</param>
    /// <param name="input">The deserialized activity input.</param>
    /// <returns>The output of the activity as a task.</returns>
    public override async Task<bool> RunAsync(WorkflowActivityContext context, string input)
    {
        Console.WriteLine($"Workflow {input} is rejected");
        Console.WriteLine("Running Reject activity...");
        await Task.Delay(TimeSpan.FromSeconds(5));
        return true;
    }
}
