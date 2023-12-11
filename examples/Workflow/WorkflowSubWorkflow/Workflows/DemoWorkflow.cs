using Dapr.Workflow;

namespace WorkflowSubWorkflow.Workflows
{
    public class DemoWorkflow : Workflow<string, bool>
    {
        public override async Task<bool> RunAsync(WorkflowContext context, string instanceId)
        {
            Console.WriteLine($"Workflow {instanceId} Started.");
            string subInstanceId = instanceId + "-sub";
            ChildWorkflowTaskOptions options = new ChildWorkflowTaskOptions(subInstanceId);
            await context.CallChildWorkflowAsync<bool>(nameof(DemoSubWorkflow), "Hello, sub-workflow", options);
            return true;
        }
    }
}
