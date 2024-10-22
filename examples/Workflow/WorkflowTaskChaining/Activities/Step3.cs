using Dapr.Workflow;

namespace WorkflowTaskChianing.Activities
{
    public class Step3 : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input)
        {
            Console.WriteLine($"Step 3: Received input: {input}.");
            return Task.FromResult<int>(input ^ 2);
        }
    }
}
