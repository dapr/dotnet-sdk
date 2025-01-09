namespace Dapr.Workflow.Analyzers.Test;

public class WorkflowRegistrationCodeFixProviderTests
{
    [Fact]
    public async Task VerifyWorkflowRegistrationCodeFix()
    {
        var code = @"
            using Dapr.Workflow;
            using Microsoft.Extensions.DependencyInjection;
            using System.Threading.Tasks;

            public static class Program
            {
            public static void Main()
            {
                var services = new ServiceCollection();

                services.AddDaprWorkflow(options =>
                {
                });
            }

            private static async Task ScheduleWorkflow(DaprWorkflowClient client)
            {        
                await client.ScheduleNewWorkflowAsync(nameof(OrderProcessingWorkflow), null, new OrderPayload());
            }
            }

            class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
            { 
            public override Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
            {
                return Task.FromResult(new OrderResult(""Order processed""));
            }
            }

            record OrderPayload { }
            record OrderResult(string message) { }
            ";

        var expectedChangedCode = @"
            using Dapr.Workflow;
            using Microsoft.Extensions.DependencyInjection;
            using System.Threading.Tasks;

            public static class Program
            {
            public static void Main()
            {
                var services = new ServiceCollection();

                services.AddDaprWorkflow(options =>
                {
                    options.RegisterWorkflow<OrderProcessingWorkflow>();
                });
            }

            private static async Task ScheduleWorkflow(DaprWorkflowClient client)
            {        
                await client.ScheduleNewWorkflowAsync(nameof(OrderProcessingWorkflow), null, new OrderPayload());
            }
            }

            class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
            { 
            public override Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
            {
                return Task.FromResult(new OrderResult(""Order processed""));
            }
            }

            record OrderPayload { }
            record OrderResult(string message) { }
            ";

        await VerifyCodeFix.RunTest<WorkflowRegistrationCodeFixProvider>(code, expectedChangedCode);
    }    
}
