using Dapr.Workflow;

namespace WorkflowTaskChaining.Activities;

internal sealed class Step2 : WorkflowActivity<int, int>
{
    /// <summary>
    /// Override to implement async (non-blocking) workflow activity logic.
    /// </summary>
    /// <param name="context">Provides access to additional context for the current activity execution.</param>
    /// <param name="input">The deserialized activity input.</param>
    /// <returns>The output of the activity as a task.</returns>
    public override Task<int> RunAsync(WorkflowActivityContext context, int input)
    {
        Console.WriteLine($@"Step 2: Received input: {input}.");
        return Task.FromResult(input + 2);
    }
}
