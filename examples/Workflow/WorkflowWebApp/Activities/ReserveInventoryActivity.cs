namespace WorkflowWebApp.Activities
{
    using System.Threading.Tasks;
    using Dapr.Client;
    using Dapr.Workflow;
    using WorkflowWebApp.Models;

    class ReserveInventoryActivity : WorkflowActivity<InventoryRequest, InventoryResult>
    {
        readonly ILogger logger;
        readonly DaprClient client;
        private static readonly string storeName = "statestore";

        public ReserveInventoryActivity(ILoggerFactory loggerFactory, DaprClient client)
        {
            this.logger = loggerFactory.CreateLogger<ReserveInventoryActivity>();
            this.client = client;
        }

        public override async Task<InventoryResult> RunAsync(WorkflowActivityContext context, InventoryRequest req)
        {
            this.logger.LogInformation(
                "Reserving inventory for order {requestId} of {quantity} {name}",
                req.RequestId,
                req.Quantity,
                req.ItemName);

            OrderPayload original;
            string originalETag;

            // Ensure that the store has items
            (original, originalETag) = await client.GetStateAndETagAsync<OrderPayload>(storeName, req.ItemName);

            // Catch for the case where the statestore isn't setup
            if (original == null)
            {
                // Not enough paperclips.
                return new InventoryResult(false, original, originalETag);
            }

            this.logger.LogInformation(
                "There are: {requestId}, {name} available for purchase",
                original.Quantity,
                original.Name);

            // See if there're enough paperclips to purchase
            if (original.Quantity >= req.Quantity)
            {
                // Simulate slow processing
                await Task.Delay(TimeSpan.FromSeconds(2));

                return new InventoryResult(true, original, originalETag);
            }

            // Not enough paperclips.
            return new InventoryResult(false, original, originalETag);

        }
    }
}
