using Dapr.Workflow;
using WorkflowFanOutFanIn.Activities;

namespace WorkflowFanOutFanIn.Workflows
{
    public class DemoWorkflow : Workflow<string, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, string input)
        {
            Task t1 = context.CallActivityAsync(nameof(NotifyActivity), "calling task 1 ...");
            Task t2 = context.CallActivityAsync(nameof(NotifyActivity), "calling task 2 ...");
            Task t3 = context.CallActivityAsync(nameof(NotifyActivity), "calling task 3 ...");
            await Task.WhenAll(t1, t2, t3);

            return "Workflow Completed.";
        }
    }
}
