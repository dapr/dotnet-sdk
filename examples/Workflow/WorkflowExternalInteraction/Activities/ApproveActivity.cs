using Dapr.Workflow;

namespace WorkflowExternalInteraction.Activities
{
    public class ApproveActivity : WorkflowActivity<string, bool>
    {
        public override async Task<bool> RunAsync(WorkflowActivityContext context, string input)
        {
            Console.WriteLine($"Workflow {input} is approved.");
            Console.WriteLine("Running Approval activity ...");
            await Task.Delay(TimeSpan.FromSeconds(5));
            return true;
        }
    }
}
