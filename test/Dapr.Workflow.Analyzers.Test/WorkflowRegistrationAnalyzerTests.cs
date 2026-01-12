using Dapr.Analyzers.Common;

namespace Dapr.Workflow.Analyzers.Test;

public sealed class WorkflowRegistrationAnalyzerTests
{
    [Fact]
    public async Task VerifyWorkflowNotRegistered()
    {
        const string testCode = """
                                                using Dapr.Workflow;
                                                using System.Threading.Tasks;
                                
                                                class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
                                                { 
                                                    public override async Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
                                                    {
                                                        return new OrderResult("Order processed");
                                                    }
                                                }
                                
                                                class UseWorkflow()
                                                {
                                                    public async Task RunWorkflow(DaprWorkflowClient client, OrderPayload order)
                                                    {
                                                        await client.ScheduleNewWorkflowAsync(nameof(OrderProcessingWorkflow), null, order);
                                                    }
                                                }
                                
                                                record OrderPayload { }
                                                record OrderResult(string message) { }  
                                """;

        var expected = VerifyAnalyzer.Diagnostic( WorkflowRegistrationAnalyzer.WorkflowDiagnosticDescriptor)
            .WithSpan(16, 63, 16, 94).WithMessage("The workflow type 'OrderProcessingWorkflow' is not registered with the dependency injection provider");

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowRegistrationAnalyzer>(testCode, expected);
    }

    [Fact]
    public async Task VerifyWorkflowRegistered()
    {
        const string testCode = """           
                                                using Dapr.Workflow;
                                                using System.Threading.Tasks;
                                
                                                class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
                                                { 
                                                    public override async Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
                                                    {
                                                        return new OrderResult("Order processed");
                                                    }
                                                }
                                
                                                record OrderPayload { }
                                                record OrderResult(string message) { }             
                                """;

        const string startupCode = """
                                                   using Dapr.Workflow;    
                                                   using Microsoft.Extensions.DependencyInjection;
                                   
                                                   internal static class Extensions
                                                   {
                                                       public static void AddApplicationServices(this IServiceCollection services)
                                                       {
                                                           services.AddDaprWorkflow(options =>
                                                           {
                                                               options.RegisterWorkflow<OrderProcessingWorkflow>();
                                                           });
                                                       }
                                                   }             
                                   """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowRegistrationAnalyzer>(testCode, startupCode);
    }
}
