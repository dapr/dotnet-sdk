# Dapr Workflow with ASP.NET Core sample

This Dapr workflow example shows how to create a Dapr workflow (`Workflow`) and invoke it using the console.

## Prerequisites

- [.NET 6+](https://dotnet.microsoft.com/download) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://github.com/dapr/dotnet-sdk/)

## Projects in sample

This sample contains a single [WorkflowConsoleApp](./WorkflowConsoleApp) ASP.NET Core project. It utilizes the workflow implementations starting and querying workflows instances.

The main `Program.cs` file contains the main setup of the app, including  the registration of the workflow and workflow activities. The workflow definition is found in `Workflows` directory and the workflow activity definitions are found in the `Activities` directory.

## Running the example

To run the workflow web app locally, two separate terminal windows are required.
In the first terminal window, down the `WorkflowConsoleApp` directory, run the following command to start the program itself:

```sh
dotnet run
```

Next, in a separate terminal window, start the dapr sidecar:

```sh
dapr run --app-id wfwebapp --dapr-grpc-port 4001 --dapr-http-port 3500
```

Dapr will listen for HTTP requests at `http://localhost:3500`.

This workflow example utilizes a redis statestore to simulate the purchasing of items and restocking of inventory. The console prompts will provide directions on how to both purchase and restock items.

To start a workflow, you have two options:
Option A: Follow the directions from the console prompts.

Option B: Use the workflows API and send a request to Dapr directly. Examples are included below as well as in the "demo.http" file down the "WorkflowConsoleApp" directory.

Two identical `curl` commands are shown, one for Linux/macOS (bash) and the other for Windows (PowerShell). The body of the request is used as the input of the workflow. 

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

If successful, you should see a response like the following, which contains a `Location` header pointing to a status endpoint for the workflow that was created with the identifier that you provided. 

```http
HTTP/1.1 202 Accepted
Date: Thu, 02 Feb 2023 23:34:53 GMT
Content-Type: application/json
```

Next, send an HTTP request to get the status of the workflow that was started:

```bash
curl -i -X GET http://localhost:3500/v1.0-alpha1/workflows/dapr/OrderProcessingWorkflow/1234
```

If the workflow has completed running, you should see the following output (formatted for readability):

```http
HTTP/1.1 202 Accepted
Date: Thu, 02 Feb 2023 23:43:27 GMT
Content-Type: application/json

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

If the workflow hasn't completed yet, you might instead see the following:

```http
HTTP/1.1 202 Accepted
Date: Thu, 02 Feb 2023 23:43:27 GMT
Content-Type: application/json

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

When the workflow has completed, the stdout of the web app should look like the following:

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
