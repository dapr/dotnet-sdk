using Dapr.Client;
using Dapr.Workflow;
using Microsoft.Extensions.Logging;

namespace WorkflowConsoleApp.Activities;

class UpdateInventoryActivity : WorkflowActivity<PaymentRequest, object>
{
    static readonly string storeName = "statestore";
    readonly ILogger logger;
    readonly DaprClient client;

    public UpdateInventoryActivity(ILoggerFactory loggerFactory, DaprClient client)
    {
        this.logger = loggerFactory.CreateLogger<UpdateInventoryActivity>();
        this.client = client;
    }

    public override async Task<object> RunAsync(WorkflowActivityContext context, PaymentRequest req)
    {
        this.logger.LogInformation(
            "Checking inventory for order '{requestId}' for {amount} {name}",
            req.RequestId,
            req.Amount,
            req.ItemName);

        // Simulate slow processing
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Determine if there are enough Items for purchase
        InventoryItem item = await client.GetStateAsync<InventoryItem>(
            storeName,
            req.ItemName.ToLowerInvariant());
        int newQuantity = item.Quantity - req.Amount;
        if (newQuantity < 0)
        {
            this.logger.LogInformation(
                "Payment for request ID '{requestId}' could not be processed. Insufficient inventory.",
                req.RequestId);
            throw new InvalidOperationException($"Not enough '{req.ItemName}' inventory! Requested {req.Amount} but only {item.Quantity} available.");
        }

        // Update the statestore with the new amount of the item
        await client.SaveStateAsync(
            storeName,
            req.ItemName.ToLowerInvariant(),
            new InventoryItem(Name: req.ItemName, PerItemCost: item.PerItemCost, Quantity: newQuantity));

        this.logger.LogInformation(
            "There are now {quantity} {name} left in stock",
            newQuantity,
            item.Name);

        return null;
    }
}