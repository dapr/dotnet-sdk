using Dapr.Workflow;

namespace WorkflowSubWorkflow.Workflows
{
    public class DemoSubWorkflow : Workflow<string, bool>
    {
        public override async Task<bool> RunAsync(WorkflowContext context, string input)
        {
            Console.WriteLine($"Workflow {context.InstanceId} Started.");
            Console.WriteLine($"Received input: {input}.");
            await context.CreateTimer(TimeSpan.FromSeconds(5));
            return true;
        }
    }
}
