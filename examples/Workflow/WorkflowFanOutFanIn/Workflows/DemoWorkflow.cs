using Dapr.Workflow;
using WorkflowFanOutFanIn.Activities;

namespace WorkflowFanOutFanIn.Workflows;

public sealed class DemoWorkflow : Workflow<string, string>
{
    /// <summary>
    /// Override to implement workflow logic.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <param name="input">The deserialized workflow input.</param>
    /// <returns>The output of the workflow as a task.</returns>
    public override async Task<string> RunAsync(WorkflowContext context, string input)
    {
        var tasks = new List<Task>();
        for (var a = 1; a <= 3; a++)
        {
            var task = context.CallActivityAsync(nameof(NotifyActivity), $"calling task {a}");
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        return "Workflow completed";
    }
}
