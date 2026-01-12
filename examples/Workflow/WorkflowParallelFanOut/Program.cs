using Dapr.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkflowParallelFanOut;

var builder = Host.CreateDefaultBuilder(args).ConfigureServices(services =>
{
    services.AddDaprWorkflow(options =>
    {
        options.RegisterWorkflow<OrderProcessingWorkflow>();
        options.RegisterActivity<ProcessOrderActivity>();
    });
});

var host = builder.Build();
await host.StartAsync();

await using var scope = host.Services.CreateAsyncScope();
var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

// Create sample orders to demonstrate parallel processing
var orders = new[]
{
    new OrderRequest("ORD-001", "Laptop", 2, 999.99m),
    new OrderRequest("ORD-002", "Mouse", 5, 29.99m),
    new OrderRequest("ORD-003", "Keyboard", 3, 79.99m),
    new OrderRequest("ORD-004", "Monitor", 1, 299.99m),
    new OrderRequest("ORD-005", "Headphones", 4, 149.99m),
    new OrderRequest("ORD-006", "Webcam", 2, 89.99m),
    new OrderRequest("ORD-007", "Printer", 1, 199.99m),
    new OrderRequest("ORD-008", "Tablet", 6, 249.99m),
    new OrderRequest("ORD-009", "Phone", 1, 799.99m),
    new OrderRequest("ORD-010", "Charger", 10, 24.99m), // Bulk order for discount
    new OrderRequest("ORD-011", "Cable", 20, 9.99m),    // Bulk order for discount
    new OrderRequest("ORD-012", "Speakers", 3, 119.99m),
    new OrderRequest("ORD-013", "Router", 2, 149.99m),
    new OrderRequest("ORD-014", "Hard Drive", 4, 129.99m),
    new OrderRequest("ORD-015", "Graphics Card", 1, 599.99m)
};

logger.LogInformation("Starting workflow with {OrderCount} orders", orders.Length);

var instanceId = $"orderprocessing-workflow-{Guid.NewGuid().ToString()[..8]}";
await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(OrderProcessingWorkflow), instanceId, orders);

logger.LogInformation("Workflow {InstanceId} started, waiting for completion...", instanceId);

// Wait for workflow completion
await daprWorkflowClient.WaitForWorkflowCompletionAsync(instanceId);
var state = await daprWorkflowClient.GetWorkflowStateAsync(instanceId);

logger.LogInformation("Workflow {InstanceId} completed with status: {Status}", instanceId, state?.RuntimeStatus);

if (state?.ReadOutputAs<OrderResult[]>() is { } results)
{
    logger.LogInformation("Processing Results:");
    logger.LogInformation("==================");

    var processedOrders = results.Where(r => r.IsProcessed).ToList();
    var failedOrders = results.Where(r => !r.IsProcessed).ToList();

    logger.LogInformation("Successfully processed {ProcessedCount} orders:", processedOrders.Count);
    foreach (var order in processedOrders)
    {
        logger.LogInformation("  - {OrderId}: {TotalAmount:C} ({Status})",
            order.OrderId, order.TotalAmount, order.Status);
    }

    if (failedOrders.Count != 0)
    {
        logger.LogWarning("Failed orders ({FailedCount}):", failedOrders.Count);
        foreach (var order in failedOrders)
        {
            logger.LogWarning("  - {OrderId}: {Status}", order.OrderId, order.Status);
        }
    }

    var totalAmount = processedOrders.Sum(r => r.TotalAmount);
    logger.LogInformation("Total processed amount: {TotalAmount:C}", totalAmount);
}
