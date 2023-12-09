using Dapr.Workflow;

namespace WorkflowExternalInteraction.Activities
{
    public class RejectActivity : WorkflowActivity<string, bool>
    {
        public override async Task<bool> RunAsync(WorkflowActivityContext context, string input)
        {
            Console.WriteLine($"Workflow {input} is rejected.");
            Console.WriteLine("Running Reject activity ...");
            await Task.Delay(TimeSpan.FromSeconds(5));
            return true;
        }
    }
}
