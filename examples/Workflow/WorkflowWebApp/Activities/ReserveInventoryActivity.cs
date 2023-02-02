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
        static readonly string storeName = "statestore";

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

            OrderPayload orderResponse;
            string key;

            // Ensure that the store has items
            (orderResponse, key) = await client.GetStateAndETagAsync<OrderPayload>(storeName, req.ItemName);

            // Catch for the case where the statestore isn't setup
            if (orderResponse == null)
            {
                // Not enough paperclips.
                return new InventoryResult(false, orderResponse);
            }

            this.logger.LogInformation(
                "There are: {requestId}, {name} available for purchase",
                orderResponse.Quantity,
                orderResponse.Name);

            // See if there're enough paperclips to purchase
            if (orderResponse.Quantity >= req.Quantity)
            {
                // Simulate slow processing
                await Task.Delay(TimeSpan.FromSeconds(2));

                return new InventoryResult(true, orderResponse);
            }

            // Not enough paperclips.
            return new InventoryResult(false, orderResponse);

        }
    }
}
