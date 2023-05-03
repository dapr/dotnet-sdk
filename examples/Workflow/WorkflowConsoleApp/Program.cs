using Dapr.Client;
using Dapr.Workflow;
using WorkflowConsoleApp.Activities;
using WorkflowConsoleApp.Models;
using WorkflowConsoleApp.Workflows;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

const string storeName = "statestore";

// The workflow host is a background service that connects to the sidecar over gRPC
var builder = Host.CreateDefaultBuilder(args).ConfigureServices(services =>
{
    services.AddDaprWorkflow(options =>
    {
        // Note that it's also possible to register a lambda function as the workflow
        // or activity implementation instead of a class.
        options.RegisterWorkflow<OrderProcessingWorkflow>();

        // These are the activities that get invoked by the workflow(s).
        options.RegisterActivity<NotifyActivity>();
        options.RegisterActivity<ReserveInventoryActivity>();
        options.RegisterActivity<ProcessPaymentActivity>();
        options.RegisterActivity<UpdateInventoryActivity>();
    });
});

// Dapr uses a random port for gRPC by default. If we don't know what that port
// is (because this app was started separate from dapr), then assume 4001.
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DAPR_GRPC_PORT")))
{
    Environment.SetEnvironmentVariable("DAPR_GRPC_PORT", "4001");
}

Console.ForegroundColor = ConsoleColor.White;
Console.WriteLine("*** Welcome to the Dapr Workflow console app sample!");
Console.WriteLine("*** Using this app, you can place orders that start workflows.");
Console.WriteLine("*** Ensure that Dapr is running in a separate terminal window using the following command:");
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("        dapr run --dapr-grpc-port 4001 --app-id wfapp");
Console.WriteLine();
Console.ResetColor();

// Start the app - this is the point where we connect to the Dapr sidecar to
// listen for workflow work-items to execute.
using var host = builder.Build();
host.Start();

using var daprClient = new DaprClientBuilder().Build();

// Wait for the sidecar to become available
while (!await daprClient.CheckHealthAsync())
{
    Thread.Sleep(TimeSpan.FromSeconds(5));
}

// Wait one more second for the workflow engine to finish initializing.
// This is just to make the log output look a little nicer.
Thread.Sleep(TimeSpan.FromSeconds(1));

DaprWorkflowClient workflowClient = host.Services.GetRequiredService<DaprWorkflowClient>();

var baseInventory = new List<InventoryItem>
{
    new InventoryItem(Name: "Paperclips", PerItemCost: 5, Quantity: 100),
    new InventoryItem(Name: "Cars", PerItemCost: 15000, Quantity: 100),
    new InventoryItem(Name: "Computers", PerItemCost: 500, Quantity: 100),
};

// Populate the store with items
await RestockInventory(daprClient, baseInventory);

// Start the input loop
while (true)
{
    // Get the name of the item to order and make sure we have inventory
    string items = string.Join(", ", baseInventory.Select(i => i.Name));
    Console.WriteLine($"Enter the name of one of the following items to order [{items}].");
    Console.WriteLine("To restock items, type 'restock'.");
    string itemName = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(itemName))
    {
        continue;
    }
    else if (string.Equals("restock", itemName, StringComparison.OrdinalIgnoreCase))
    {
        await RestockInventory(daprClient, baseInventory);
        continue;
    }

    InventoryItem item = baseInventory.FirstOrDefault(item => string.Equals(item.Name, itemName, StringComparison.OrdinalIgnoreCase));
    if (item == null)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"We don't have {itemName}!");
        Console.ResetColor();
        continue;
    }

    Console.WriteLine($"How many {itemName} would you like to purchase?");
    string amountStr = Console.ReadLine().Trim();
    if (!int.TryParse(amountStr, out int amount) || amount <= 0)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Invalid input. Assuming you meant to type '1'.");
        Console.ResetColor();
        amount = 1;
    }

    // Construct the order with a unique order ID
    string orderId = $"{itemName.ToLowerInvariant()}-{Guid.NewGuid().ToString()[..8]}";
    double totalCost = amount * item.PerItemCost;
    var orderInfo = new OrderPayload(itemName.ToLowerInvariant(), totalCost, amount);

    // Start the workflow using the order ID as the workflow ID
    Console.WriteLine($"Starting order workflow '{orderId}' purchasing {amount} {itemName}");
    await workflowClient.ScheduleNewWorkflowAsync(
        name: nameof(OrderProcessingWorkflow),
        instanceId: orderId,
        input: orderInfo);

    // Wait for the workflow to complete
    WorkflowState state = await workflowClient.WaitForWorkflowCompletionAsync(
        instanceId: orderId,
        getInputsAndOutputs: true);

    if (state.RuntimeStatus == WorkflowRuntimeStatus.Completed)
    {
        OrderResult result = state.ReadOutputAs<OrderResult>();
        if (result.Processed)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Order workflow is {state.RuntimeStatus} and the order was processed successfully.");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine($"Order workflow is {state.RuntimeStatus} but the order was not processed.");
        }
    }
    else if (state.RuntimeStatus == WorkflowRuntimeStatus.Failed)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"The workflow failed - {state.FailureDetails}");
        Console.ResetColor();
    }

    Console.WriteLine();
}

static async Task RestockInventory(DaprClient daprClient, List<InventoryItem> inventory)
{
    Console.WriteLine("*** Restocking inventory...");
    foreach (var item in inventory)
    {
        Console.WriteLine($"*** \t{item.Name}: {item.Quantity}");
        await daprClient.SaveStateAsync(storeName, item.Name.ToLowerInvariant(), item);
    }
}
