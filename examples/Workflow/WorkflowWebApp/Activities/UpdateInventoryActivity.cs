namespace WorkflowWebApp.Activities
{
    using System.Threading.Tasks;
    using Dapr.Client;
    using Dapr.Workflow;
    using WorkflowWebApp.Models;
    using DurableTask.Core.Exceptions;

    class UpdateInventoryActivity : WorkflowActivity<PaymentRequest, Object>
    {
        readonly ILogger logger;
        readonly DaprClient client;
        private static readonly string storeName = "statestore";

        public UpdateInventoryActivity(ILoggerFactory loggerFactory, DaprClient client)
        {
            this.logger = loggerFactory.CreateLogger<UpdateInventoryActivity>();
            this.client = client;
        }

        public override async Task<Object> RunAsync(WorkflowActivityContext context, PaymentRequest req)
        {
            this.logger.LogInformation(
                "Checking Inventory for: Order# {requestId} for {amount} {item}",
                req.RequestId,
                req.Amount,
                req.ItemBeingPruchased);

            // Simulate slow processing
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Determine if there are enough Paperclips for purchase
            var (original, originalETag) = await client.GetStateAndETagAsync<OrderPayload>(storeName, req.ItemBeingPruchased);
            var newQuantity = original.Quantity - req.Amount;
            
            if (newQuantity < 0)
            {
                this.logger.LogInformation(
                    "Payment for request ID '{requestId}' could not be processed. Insufficient inventory.",
                    req.RequestId);
                throw new TaskFailedException();
            }

            // Update the statestore with the new amount of paper clips
            await client.SaveStateAsync<OrderPayload>(storeName, req.ItemBeingPruchased,  new OrderPayload(Name: req.ItemBeingPruchased, TotalCost: req.Currency, Quantity: newQuantity));
            (original, originalETag) = await client.GetStateAndETagAsync<OrderPayload>(storeName, req.ItemBeingPruchased);
            this.logger.LogInformation($"There are now: {original.Quantity} {original.Name} left in stock");

            this.logger.LogInformation(
                "OrderID '{requestId}' processed successfully",
                req.RequestId);

            return null;
        }
    }
}
