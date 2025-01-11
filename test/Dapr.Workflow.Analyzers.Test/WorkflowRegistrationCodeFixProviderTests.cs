namespace Dapr.Workflow.Analyzers.Test;

public class WorkflowRegistrationCodeFixProviderTests
{
    [Fact]
    public async Task RegisterWorkflow()
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

    [Fact]
    public async Task RegisterWorkflow_WhenAddDaprWorkflowIsNotFound()
    {
        var code = @"
            using Dapr.Workflow;
            using Microsoft.AspNetCore.Builder;
            using System.Threading.Tasks;

            public static class Program
            {
            public static void Main()
            {
                var builder = WebApplication.CreateBuilder();

                var app = builder.Build();
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
            using Microsoft.AspNetCore.Builder;
            using System.Threading.Tasks;

            public static class Program
            {
            public static void Main()
            {
                var builder = WebApplication.CreateBuilder();

                builder.Services.AddDaprWorkflow(options =>
                {
                    options.RegisterWorkflow<OrderProcessingWorkflow>();
                });

                var app = builder.Build();
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

    [Fact]
    public async Task RegisterWorkflow_WhenAddDaprWorkflowIsNotFound_TopLevelStatements()
    {
        var code = @"            
            using Dapr.Workflow;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.Extensions.DependencyInjection;
            using System.Threading.Tasks;

            var builder = WebApplication.CreateBuilder();
                
            var app = builder.Build();

            var workflowClient = app.Services.GetRequiredService<DaprWorkflowClient>();
            await workflowClient.ScheduleNewWorkflowAsync(nameof(TestNamespace.OrderProcessingWorkflow), null, new OrderPayload());

            namespace TestNamespace
            {
                class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
                { 
                    public override Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
                    {
                        return Task.FromResult(new OrderResult(""Order processed""));
                    }
                }
            }

            record OrderPayload { }
            record OrderResult(string message) { }
            ";

        var expectedChangedCode = @"            
            using Dapr.Workflow;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.Extensions.DependencyInjection;
            using System.Threading.Tasks;

            var builder = WebApplication.CreateBuilder();

            builder.Services.AddDaprWorkflow(options =>
            {
                options.RegisterWorkflow<TestNamespace.OrderProcessingWorkflow>();
            });
                
            var app = builder.Build();

            var workflowClient = app.Services.GetRequiredService<DaprWorkflowClient>();
            await workflowClient.ScheduleNewWorkflowAsync(nameof(TestNamespace.OrderProcessingWorkflow), null, new OrderPayload());

            namespace TestNamespace
            {
                class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
                { 
                    public override Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
                    {
                        return Task.FromResult(new OrderResult(""Order processed""));
                    }
                }                
            }

            record OrderPayload { }
            record OrderResult(string message) { }
            ";

        await VerifyCodeFix.RunTest<WorkflowRegistrationCodeFixProvider>(code, expectedChangedCode);
    }
}
