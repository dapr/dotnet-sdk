using Dapr.Workflow;

namespace WorkflowMonitor.Activities
{
    public class CheckStatus : WorkflowActivity<bool, string>
    {
        static List<string> status = new List<string>() { "healthy", "unhealthy" };
        Random random = new Random();
        public override Task<string> RunAsync(WorkflowActivityContext context, bool input)
        {
            return Task.FromResult<string>(status[random.Next(status.Count)]);
        }
    }
}
