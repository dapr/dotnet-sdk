using Dapr.Workflow;
using WorkflowExternalInteraction.Activities;

namespace WorkflowExternalInteraction.Workflows
{
    public class DemoWorkflow : Workflow<string, bool>
    {
        public override async Task<bool> RunAsync(WorkflowContext context, string input)
        {
            try
            {
                await context.WaitForExternalEventAsync<bool>(
                            eventName: "Approval",
                            timeout: TimeSpan.FromSeconds(15));
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Approval timeout.");
                await context.CallActivityAsync(nameof(RejectActivity), input);
                Console.WriteLine("Reject Activity finished");
                return false;
            }


            await context.CallActivityAsync(nameof(ApproveActivity), input);
            Console.WriteLine("Approve Activity finished");

            return true;
        }
    }
}