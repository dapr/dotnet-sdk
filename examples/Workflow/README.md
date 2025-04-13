# Dapr Workflow with ASP.NET Core sample

This Dapr workflow example shows how to create a Dapr workflow (`Workflow`) and invoke it using the console.

## Prerequisites

- [.NET 8+](https://dotnet.microsoft.com/download) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://github.com/dapr/dotnet-sdk/)


## Optional Setup
Dapr workflow, as well as this example program, now support authentication through the use of API tokens. For more information on this, view the following document: [API Token](https://github.com/dapr/dotnet-sdk/blob/master/docs/api-tokens.md)

## Projects in sample

This sample contains a single [WorkflowConsoleApp](./WorkflowConsoleApp) .NET project.
It utilizes the workflow SDK as well as the workflow management API for simulating inventory management and sale of goods in a store.
The main `Program.cs` file contains the main setup of the app, the registration of the workflow and its activities, and interaction with the user. The workflow definition is found in the `Workflows` directory and the workflow activity definitions are found in the `Activities` directory.

There are five activities in the directory that could be called by the workflows:
- `NotifyActivity`:  printing logs as notifications
- `ProcessPaymentActivity`: printing logs and delaying for simulating payment processing
- `RequestApprovalActivity`: printing logs to indicate that the order has been approved
- `ReserveInventoryActivity`: checking if there are enough items for purchase
- `UpdateInventoryActivity`: updating the statestore according to purchasing

The `OrderProcessingWorkflow.cs` in `Workflows` directory implements the running logic of the workflow. Based on the purchase stage and outcome, it calls different activities and waits for the corresponding events to trigger interaction with the user.

This sample also contains a [WorkflowUnitTest](./WorkflowUnitTest) .NET project that utilizes [xUnit](https://xunit.net/) and [Moq](https://github.com/moq/moq) to test the workflow logic.
It works by creating an instance of the `OrderProcessingWorkflow` (defined in the `WorkflowConsoleApp` project), mocking activity calls, and testing the inputs and outputs.
The tests also verify that outputs of the workflow.

## Running the console app example

To run the workflow web app locally, two separate terminal windows are required.
In the first terminal window, from the `WorkflowConsoleApp` directory, run the following command to start the program itself:

```sh
dotnet run
```

Next, in a separate terminal window, start the dapr sidecar:

```sh
dapr run --app-id wfapp --dapr-grpc-port 4001 --dapr-http-port 3500
```

Dapr listens for HTTP requests at `http://localhost:3500`.

This example illustrates a purchase order processing workflow. The console prompts provide directions on how to both purchase and restock items.

To start a workflow, you have two options:

1. Follow the directions from the console prompts.
2. Use the workflows API and send a request to Dapr directly. Examples are included below as well as in the "demo.http" file down the "WorkflowConsoleApp" directory.

For the workflow API option, two identical `curl` commands are shown, one for Linux/macOS (bash) and the other for Windows (PowerShell). The body of the request is the purchase order information used as the input of the workflow. 

Make note of the "1234" in the commands below. This represents the unique identifier for the workflow run and can be replaced with any identifier of your choosing.

```bash
curl -i -X POST http://localhost:3500/v1.0-beta1/workflows/dapr/OrderProcessingWorkflow/start?instanceID=1234 \
  -H "Content-Type: application/json" \
  -d '{"Name": "Paperclips", "TotalCost": 99.95, "Quantity": 1}'
```

On Windows (PowerShell):

```powershell
curl -i -X POST http://localhost:3500/v1.0-beta1/workflows/dapr/OrderProcessingWorkflow/start?instanceID=1234 `
  -H "Content-Type: application/json" `
  -d '{"Name": "Paperclips", "TotalCost": 99.95, "Quantity": 1}'
```

If successful, you should see a response like the following: 

```json
{"instanceID":"1234"}
```

Next, send an HTTP request to get the status of the workflow that was started:

```bash
curl -i -X GET http://localhost:3500/v1.0-beta1/workflows/dapr/1234
```

The workflow is designed to take several seconds to complete. If the workflow hasn't completed yet when you issue the previous command, you should see the following JSON response (formatted for readability):

```json
{
  "instanceID": "1234",
  "workflowName": "OrderProcessingWorkflow",
  "createdAt": "2023-05-10T00:42:03.911444105Z",
  "lastUpdatedAt": "2023-05-10T00:42:06.142214153Z",
  "runtimeStatus": "RUNNING",
  "properties": {
    "dapr.workflow.custom_status": "",
    "dapr.workflow.input": "{\"Name\": \"Paperclips\", \"TotalCost\": 99.95, \"Quantity\": 1}"
  }
}
```

Once the workflow has completed running, you should see the following output, indicating that it has reached the "COMPLETED" status:

```json
{
  "instanceID": "1234",
  "workflowName": "OrderProcessingWorkflow",
  "createdAt": "2023-05-10T00:42:03.911444105Z",
  "lastUpdatedAt": "2023-05-10T00:42:18.527704176Z",
  "runtimeStatus": "COMPLETED",
  "properties": {
    "dapr.workflow.custom_status": "",
    "dapr.workflow.input": "{\"Name\": \"Paperclips\", \"TotalCost\": 99.95, \"Quantity\": 1}",
    "dapr.workflow.output": "{\"Processed\":true}"
  }
}
```

When the workflow has completed, the stdout of the workflow app should look like the following:

```log
info: WorkflowConsoleApp.Activities.NotifyActivity[0]
      Received order 1234 for Paperclips at $99.95
info: WorkflowConsoleApp.Activities.ReserveInventoryActivity[0]
      Reserving inventory: 1234, Paperclips, 1
info: WorkflowConsoleApp.Activities.ProcessPaymentActivity[0]
      Processing payment: 1234, 99.95, USD
info: WorkflowConsoleApp.Activities.NotifyActivity[0]
      Order 1234 processed successfully!
```

If you have Zipkin configured for Dapr locally on your machine, then you can view the workflow trace spans in the Zipkin web UI (typically at http://localhost:9411/zipkin/).