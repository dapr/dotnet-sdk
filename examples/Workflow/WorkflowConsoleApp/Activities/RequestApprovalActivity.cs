using Dapr.Workflow;
using Microsoft.Extensions.Logging;

namespace WorkflowConsoleApp.Activities;

public class RequestApprovalActivity : WorkflowActivity<OrderPayload, object>
{
    readonly ILogger logger;

    public RequestApprovalActivity(ILoggerFactory loggerFactory)
    {
        this.logger = loggerFactory.CreateLogger<RequestApprovalActivity>();
    }

    public override Task<object> RunAsync(WorkflowActivityContext context, OrderPayload input)
    {
        string orderId = context.InstanceId.ToString();
        this.logger.LogInformation("Requesting approval for order {orderId}", orderId);

        return Task.FromResult<object>(null);
    }
}