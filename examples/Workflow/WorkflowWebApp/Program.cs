using Dapr.Client;
using Dapr.Workflow;
using WorkflowWebApp.Activities;
using WorkflowWebApp.Models;
using WorkflowWebApp.Workflows;
using System.Text.Json;
using Microsoft.Extensions.Hosting;

const string workflowComponent = "dapr";
const string storeName = "statestore";
const string workflowName = nameof(OrderProcessingWorkflow);

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

// Start the app - this is the point where we connect to the Dapr sidecar
using var host = builder.Build();
host.Start();

// Start the client
string daprPortStr = Environment.GetEnvironmentVariable("DAPR_GRPC_PORT");
if (string.IsNullOrEmpty(daprPortStr))
{
    Environment.SetEnvironmentVariable("DAPR_GRPC_PORT", "4001");
}
using var daprClient = new DaprClientBuilder().Build();

// Start the console app
while (true)
{
    var health = await daprClient.CheckHealthAsync();
    Console.WriteLine("Welcome to the workflows example!");
    Console.WriteLine("In this example, you will be starting a workflow and obtaining the status.");
    Console.WriteLine("First, ensure that dapr is running and connected to the correct port");
    Console.WriteLine("To do this, run the following command in a separate teminal window: dapr run --dapr-grpc-port 4001 --app-id wfwebapp");

    // tell the user whats already in store
    Console.WriteLine("For starters, the store has been stocked with 100 'Cars', 100 'Paperclips' and 100 'Computers':");
    // Populate the store with items
    RestockInventory();
    
    // Main Loop
    string continueChar = "1";
    while (continueChar == "1")
    {
        // Generate a unique ID for the workflow
        string orderId = Guid.NewGuid().ToString()[..8];
        Console.WriteLine("To get started, enter an item you would like to purchase:");
        var itemToPurchase = Console.ReadLine();
        Console.WriteLine("You are going to purchase: {0}", itemToPurchase);
        Console.WriteLine("Next, enter the quantity of {0} that you want to purchase: ", itemToPurchase);
        var ammountToPurchase = Convert.ToInt32(Console.ReadLine());

        // Construct the order
        OrderPayload orderInfo = new OrderPayload(itemToPurchase, 99.95, ammountToPurchase);

        Console.WriteLine("Before check inventory!");

        OrderPayload orderResponse;
        string key;
        // Ensure that the store has items
        (orderResponse, key) = await daprClient.GetStateAndETagAsync<OrderPayload>(storeName, itemToPurchase);

        Console.WriteLine("After check inventory: {0}", orderResponse);

        // Start the workflow
        Console.WriteLine("Starting workflow {0} purchasing {1} {2}", orderId, ammountToPurchase, itemToPurchase);
        var response = await daprClient.StartWorkflowAsync(orderId, workflowComponent, workflowName, orderInfo, null, CancellationToken.None);

        var state = await daprClient.GetWorkflowAsync(orderId, workflowComponent, workflowName);
        Console.WriteLine("Your workflow has started. Here is the status of the workflow: {0}", state);
        while (state.metadata["dapr.workflow.runtime_status"].ToString() == "RUNNING")
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            Console.WriteLine("Your workflow is in progress. Here is the status of the workflow: {0}", state);
            state = await daprClient.GetWorkflowAsync(orderId, workflowComponent, workflowName);
        }
        Console.WriteLine("Your workflow has completed: {0}", JsonSerializer.Serialize(state));
        Console.WriteLine("To start another workflow, press 1. If you'd like to restock the store, press 2. Otherwise, you can exit the demo with any other character");
        continueChar = Console.ReadLine();

        if (continueChar == "2")
        {
            RestockInventory();
            continueChar = "1";
        }
    }
    
    break;
}

void RestockInventory()
{
    daprClient.SaveStateAsync<OrderPayload>(storeName, "Paperclips",  new OrderPayload(Name: "PaperClips", TotalCost: 5, Quantity: 100));
    daprClient.SaveStateAsync<OrderPayload>(storeName, "Cars",  new OrderPayload(Name: "Cars", TotalCost: 15000, Quantity: 100));
    daprClient.SaveStateAsync<OrderPayload>(storeName, "Computers",  new OrderPayload(Name: "Computers", TotalCost: 500, Quantity: 100));
    return;
}
