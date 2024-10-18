using Dapr.Workflow;

namespace WorkflowTaskChianing.Activities
{
    public class Step2 : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input)
        {
            Console.WriteLine($"Step 2: Received input: {input}.");
            return Task.FromResult<int>(input * 2);
        }
    }
}
