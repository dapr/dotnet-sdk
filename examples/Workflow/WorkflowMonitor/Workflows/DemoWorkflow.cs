using Dapr.Workflow;
using WorkflowMonitor.Activities;

namespace WorkflowMonitor.Workflows
{
    public class DemoWorkflow : Workflow<bool, bool>
    {
        public override async Task<bool> RunAsync(WorkflowContext context, bool isHealthy)
        {
            string status = await context.CallActivityAsync<string>(nameof(CheckStatus), true);
            int next_sleep_interval;
            if (!context.IsReplaying)
            {
                Console.WriteLine($"This job is {status}");
            }

            if (status == "healthy")
            {
                isHealthy = true;
                next_sleep_interval = 30;
            }
            else
            {
                if (isHealthy)
                {
                    isHealthy = false;
                }
                Console.WriteLine($"Status is unhealthy. Set check interval to 1s");
                next_sleep_interval = 5;
            }

            await context.CreateTimer(TimeSpan.FromSeconds(next_sleep_interval));
            context.ContinueAsNew(isHealthy);

            // This workflow will never complete.
            return true;
        }
    }
}
