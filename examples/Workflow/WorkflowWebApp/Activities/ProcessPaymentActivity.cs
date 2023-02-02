namespace WorkflowWebApp.Activities
{
    using System.Threading.Tasks;
    using Dapr.Client;
    using Dapr.Workflow;
    using WorkflowWebApp.Models;

    class ProcessPaymentActivity : WorkflowActivity<PaymentRequest, object>
    {
        readonly ILogger logger;
        readonly DaprClient client;

        public ProcessPaymentActivity(ILoggerFactory loggerFactory, DaprClient client)
        {
            this.logger = loggerFactory.CreateLogger<ProcessPaymentActivity>();
            this.client = client;
        }

        public override async Task<object> RunAsync(WorkflowActivityContext context, PaymentRequest req)
        {
            this.logger.LogInformation(
                "Processing payment: {requestId} for {amount} {item} at ${currency}",
                req.RequestId,
                req.Amount,
                req.ItemBeingPruchased,
                req.Currency);

            // Simulate slow processing
            await Task.Delay(TimeSpan.FromSeconds(7));

            this.logger.LogInformation(
                "Payment for request ID '{requestId}' processed successfully",
                req.RequestId);

            return null;
        }
    }
}
