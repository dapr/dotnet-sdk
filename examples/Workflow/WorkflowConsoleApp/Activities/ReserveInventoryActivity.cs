using Dapr.Client;
using Dapr.Workflow;
using Microsoft.Extensions.Logging;

namespace WorkflowConsoleApp.Activities;

public class ReserveInventoryActivity : WorkflowActivity<InventoryRequest, InventoryResult>
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
            "Reserving inventory for order '{requestId}' of {quantity} {name}",
            req.RequestId,
            req.Quantity,
            req.ItemName);

        // Ensure that the store has items
        InventoryItem item = await this.client.GetStateAsync<InventoryItem>(
            storeName,
            req.ItemName.ToLowerInvariant());

        // Catch for the case where the statestore isn't setup
        if (item == null)
        {
            // Not enough items.
            return new InventoryResult(false, item);
        }

        this.logger.LogInformation(
            "There are {quantity} {name} available for purchase",
            item.Quantity,
            item.Name);

        // See if there're enough items to purchase
        if (item.Quantity >= req.Quantity)
        {
            // Simulate slow processing
            await Task.Delay(TimeSpan.FromSeconds(2));

            return new InventoryResult(true, item);
        }

        // Not enough items.
        return new InventoryResult(false, item);

    }
}