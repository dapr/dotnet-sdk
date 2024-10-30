using Dapr.Workflow;

namespace WorkflowFanOutFanIn.Activities;

internal sealed class NotifyActivity : WorkflowActivity<string, object?>
{
    /// <summary>
    /// Override to implement async (non-blocking) workflow activity logic.
    /// </summary>
    /// <param name="context">Provides access to additional context for the current activity execution.</param>
    /// <param name="input">The deserialized activity input.</param>
    /// <returns>The output of the activity as a task.</returns>
    public override Task<object?> RunAsync(WorkflowActivityContext context, string input)
    {
        Console.WriteLine(input);
        return Task.FromResult<object?>(null);
    }
}
