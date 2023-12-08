using Dapr.Workflow;

namespace WorkflowFanOutFanIn.Activities
{
    public class NotifyActivity : WorkflowActivity<string, object>
    {
        public override Task<object> RunAsync(WorkflowActivityContext context, string message)
        {
            Console.WriteLine(message);
            return Task.FromResult<object>(null);
        }
    }
}
