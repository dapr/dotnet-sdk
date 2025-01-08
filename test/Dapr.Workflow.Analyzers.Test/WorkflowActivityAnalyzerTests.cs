using Microsoft.CodeAnalysis;

namespace Dapr.Workflow.Analyzers.Test;

public class WorkflowActivityAnalyzerTests
{
    [Fact]
    public async Task VerifyNotifyActivityNotRegistered()
    {
        var testCode = @"
using Dapr.Workflow;
using System.Threading.Tasks;

class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
{ 
    public override async Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order) 
    { 
        await context.CallActivityAsync(nameof(NotifyActivity), new Notification(""Order received""));
        return new OrderResult(""Order processed"");
    } 
}

class OrderPayload { } 
class OrderResult(string message) { }
class Notification { public Notification(string message) { } }
class NotifyActivity { }
";

        var expected = Verify.Diagnostic("DAPR1001", DiagnosticSeverity.Warning)
            .WithSpan(9, 41, 9, 63).WithMessage("The class 'NotifyActivity' is not registered in the DI container");

        await Verify.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task VerifyNotifyActivityRegistered()
    {
        var testCode = @"
    using Dapr.Workflow;
    using Microsoft.Extensions.DependencyInjection;
    using System.Threading.Tasks;

    class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
    { 
        public override async Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
        { 
            await context.CallActivityAsync(nameof(NotifyActivity), new Notification(""Order received""));
            return new OrderResult(""Order processed"");
        } 
    }

    record OrderPayload { } 
    record OrderResult(string message) { }
    record Notification(string Message);

    class NotifyActivity : WorkflowActivity<Notification, object?>
    {

        public override Task<object?> RunAsync(WorkflowActivityContext context, Notification notification)
        {
            return Task.FromResult<object?>(null);
        }
    }
    ";

        var startupCode = @"
    using Dapr.Workflow;    
    using Microsoft.Extensions.DependencyInjection;

    internal static class Extensions
    {
        public static void AddApplicationServices(this IServiceCollection services)
        {
            services.AddDaprWorkflow(options =>
            {
                options.RegisterActivity<NotifyActivity>();
            });
        }
    }
    ";

        await Verify.VerifyAnalyzerAsync(testCode, startupCode);
    }
}
