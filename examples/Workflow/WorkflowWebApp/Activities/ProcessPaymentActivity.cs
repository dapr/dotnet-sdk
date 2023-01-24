namespace WorkflowWebApp.Activities
{
    using System.Threading.Tasks;
    using Dapr.Workflow;

    record PaymentRequest(string RequestId, double Amount, string Currency);

    class ProcessPaymentActivity : WorkflowActivity<PaymentRequest, object>
    {
        readonly ILogger logger;

        public ProcessPaymentActivity(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger<ProcessPaymentActivity>();
        }

        public override async Task<object> RunAsync(WorkflowActivityContext context, PaymentRequest req)
        {
            this.logger.LogInformation(
                "Processing payment: {requestId}, {amount}, {currency}",
                req.RequestId,
                req.Amount,
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
