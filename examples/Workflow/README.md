# Dapr Workflow with ASP.NET Core sample

This Dapr workflow example shows how to create a Dapr workflow (`Workflow`) and invoke it using the console.

## Prerequisites

- [.NET 6+](https://dotnet.microsoft.com/download) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://github.com/dapr/dotnet-sdk/)

## Projects in sample

This sample contains a single [WorkflowConsoleApp](./WorkflowConsoleApp) .NET project.
It utilizes the workflow SDK as well as the workflow management API for starting and querying workflows instances.
The main `Program.cs` file contains the main setup of the app, including  the registration of the workflow and workflow activities.
The workflow definition is found in the `Workflows` directory and the workflow activity definitions are found in the `Activities` directory.

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
curl -i -X POST http://localhost:3500/v1.0-alpha1/workflows/dapr/OrderProcessingWorkflow/1234/start \
  -H "Content-Type: application/json" \
  -d '{ "input" : {"Name": "Paperclips", "TotalCost": 99.95, "Quantity": 1}}'
```

On Windows (PowerShell):

```powershell
curl -i -X POST http://localhost:3500/v1.0-alpha1/workflows/dapr/OrderProcessingWorkflow/1234/start `
  -H "Content-Type: application/json" `
  -d '{ "input" : {"Name": "Paperclips", "TotalCost": 99.95, "Quantity": 1}}'
```

If successful, you should see a response like the following: 

```json
{"instance_id":"1234"}
```

Next, send an HTTP request to get the status of the workflow that was started:

```bash
curl -i -X GET http://localhost:3500/v1.0-alpha1/workflows/dapr/OrderProcessingWorkflow/1234
```

The workflow is designed to take several seconds to complete. If the workflow hasn't completed yet when you issue the previous command, you should see the following JSON response (formatted for readability):

```json
{
  "WFInfo": {
    "instance_id": "1234"
  },
  "start_time": "2023-02-02T23:34:53Z",
  "metadata": {
    "dapr.workflow.custom_status": "",
    "dapr.workflow.input": "{\"Name\":\"Paperclips\",\"Quantity\":1,\"TotalCost\":99.95}",
    "dapr.workflow.last_updated": "2023-02-02T23:35:07Z",
    "dapr.workflow.name": "OrderProcessingWorkflow",
    "dapr.workflow.output": "{\"Processed\":true}",
    "dapr.workflow.runtime_status": "RUNNING"
  }
}
```

Once the workflow has completed running, you should see the following output, indicating that it has reached the "COMPLETED" status:

```json
{
  "WFInfo": {
    "instance_id": "1234"
  },
  "start_time": "2023-02-02T23:34:53Z",
  "metadata": {
    "dapr.workflow.custom_status": "",
    "dapr.workflow.input": "{\"Name\":\"Paperclips\",\"Quantity\":1,\"TotalCost\":99.95}",
    "dapr.workflow.last_updated": "2023-02-02T23:35:07Z",
    "dapr.workflow.name": "OrderProcessingWorkflow",
    "dapr.workflow.output": "{\"Processed\":true}",
    "dapr.workflow.runtime_status": "COMPLETED"
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
