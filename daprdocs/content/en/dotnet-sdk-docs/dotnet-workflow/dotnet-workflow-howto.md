---
type: docs
title: "How to: Author and manage Dapr Workflow in the .NET SDK"
linkTitle: "How to: Author & manage workflows"
weight: 100000
description: Learn how to author and manage Dapr Workflow using the .NET SDK
---

Let's create a Dapr workflow (`Workflow`) and invoke it using the console. In the provided purchase order processing workflow example, the console prompts provide directions on how to both purchase and restock items. In this guide, you will:

- Create an ASP.NET application ([WorkflowConsoleApp](./WorkflowConsoleApp)). 
- Utilize the .NET workflow SDK and API calls to start and query workflow instances.

In the .NET example project:
- The main `Program.cs` file contains the setup of the app, including the registration of the workflow and workflow activities. 
- The workflow definition is found in the `Workflows` directory. 
- The workflow activity definitions are found in the `Activities` directory.

## Prerequisites

- [.NET 6+](https://dotnet.microsoft.com/download) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://github.com/dapr/dotnet-sdk/)

## Run the application

You'll need two terminal windows to run the workflow web app locally.

In the first terminal window, from the `WorkflowConsoleApp` directory, run the following command to start the program itself:

```sh
dotnet run
```

Next, in a separate terminal window, start the Dapr sidecar:

```sh
dapr run --app-id wfapp --dapr-grpc-port 4001 --dapr-http-port 3500
```

Dapr listens for HTTP requests at `http://localhost:3500`.

## Start a workflow

To start a workflow, you have two options:

1. Follow the directions from the console prompts.
1. Use the workflow API and send a request to Dapr directly. 

This guide focuses on the workflow API option. Examples are included both below and in the `WorkflowConsoleApp`>`demo.http` file.

Run the following command to start a workflow. 

{{< tabs "Linux/MacOS" "Windows">}}

{{% codetab %}}

```bash
curl -i -X POST http://localhost:3500/v1.0-alpha1/workflows/dapr/OrderProcessingWorkflow/1234/start \
  -H "Content-Type: application/json" \
  -d '{ "input" : {"Name": "Paperclips", "TotalCost": 99.95, "Quantity": 1}}'
```

{{% /codetab %}}

{{% codetab %}}

```powershell
curl -i -X POST http://localhost:3500/v1.0-alpha1/workflows/dapr/OrderProcessingWorkflow/1234/start `
  -H "Content-Type: application/json" `
  -d '{ "input" : {"Name": "Paperclips", "TotalCost": 99.95, "Quantity": 1}}'
```

{{% /codetab %}}

{{< /tabs >}}

Things to note:

- The body of the curl request is the purchase order information used as the input of the workflow. 
- The "1234" in the commands represents the unique identifier for the workflow run and can be replaced with any identifier of your choosing.


If successful, you should see a response like the following: 

```json
{"instance_id":"1234"}
```

Send an HTTP request to get the status of the workflow that was started:

```bash
curl -i -X GET http://localhost:3500/v1.0-alpha1/workflows/dapr/OrderProcessingWorkflow/1234
```

The workflow is designed to take several seconds to complete. If the workflow hasn't completed when you issue the HTTP request, you'll see the following JSON response (formatted for readability) with workflow status as `RUNNING`:

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

Once the workflow has completed running, you should see the following output, indicating that it has reached the `COMPLETED` status:

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

When the workflow has completed, the stdout of the workflow app should look like:

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

## Next steps

- [Try the Dapr Workflow quickstart]({{< ref workflow-quickstart.md >}})
- [Learn more about Dapr Workflow]({{< ref workflow-overview.md >}})