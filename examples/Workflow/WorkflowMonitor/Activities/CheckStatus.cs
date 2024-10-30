using Dapr.Workflow;

namespace WorkflowMonitor.Activities;

internal sealed class CheckStatus : WorkflowActivity<bool, string>
{
    private static List<string> status = new List<string> { "healthy", "unhealthy" };
    private Random random = new();

    /// <summary>
    /// Override to implement async (non-blocking) workflow activity logic.
    /// </summary>
    /// <param name="context">Provides access to additional context for the current activity execution.</param>
    /// <param name="input">The deserialized activity input.</param>
    /// <returns>The output of the activity as a task.</returns>
    public override Task<string> RunAsync(WorkflowActivityContext context, bool input) =>
        Task.FromResult<string>(status[random.Next(status.Count)]);
}
