using Dapr.Workflow;
using WorkflowTaskChaining.Activities;

namespace WorkflowTaskChaining.Workflows;

internal sealed class DemoWorkflow : Workflow<int, int[]>
{
    /// <summary>
    /// Override to implement workflow logic.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <param name="input">The deserialized workflow input.</param>
    /// <returns>The output of the workflow as a task.</returns>
    public override async Task<int[]> RunAsync(WorkflowContext context, int input)
    {
        var result1 = await context.CallActivityAsync<int>(nameof(Step1), input);
        var result2 = await context.CallActivityAsync<int>(nameof(Step2), result1);
        var result3 = await context.CallActivityAsync<int>(nameof(Step3), result2);
        var ret = new int[] { result1, result2, result3 };

        return ret;
    }
}
