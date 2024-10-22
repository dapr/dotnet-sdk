using Dapr.Workflow;
using WorkflowTaskChianing.Activities;

namespace WorkflowTaskChianing.Workflows
{
    public class DemoWorkflow : Workflow<int, int[]>
    {
        public override async Task<int[]> RunAsync(WorkflowContext context, int input)
        {
            int result1 = await context.CallActivityAsync<int>(nameof(Step1), input);
            int result2 = await context.CallActivityAsync<int>(nameof(Step2), result1);
            int result3 = await context.CallActivityAsync<int>(nameof(Step3), result2);
            int[] ret = new int[] { result1, result2, result3 };

            return ret;
        }
    }
}
