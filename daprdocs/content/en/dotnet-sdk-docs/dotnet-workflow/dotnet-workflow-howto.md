---
type: docs
title: "How to: Author and manage Dapr Workflow in the .NET SDK"
linkTitle: "How to: Author & manage workflows"
weight: 100000
description: Learn how to author and manage Dapr Workflow using the .NET SDK
---

Let's create a Dapr workflow and invoke it using the console. In the [provided order processing workflow example](https://github.com/dapr/dotnet-sdk/tree/master/examples/Workflow), the console prompts provide directions on how to both purchase and restock items. In this guide, you will:

- Deploy a .NET console application ([WorkflowConsoleApp](https://github.com/dapr/dotnet-sdk/tree/master/examples/Workflow/WorkflowConsoleApp)).  
- Utilize the .NET workflow SDK and API calls to start and query workflow instances.

In the .NET example project:
- The main [`Program.cs`](https://github.com/dapr/dotnet-sdk/blob/master/examples/Workflow/WorkflowConsoleApp/Program.cs) file contains the setup of the app, including the registration of the workflow and workflow activities. 
- The workflow definition is found in the [`Workflows` directory](https://github.com/dapr/dotnet-sdk/tree/master/examples/Workflow/WorkflowConsoleApp/Workflows). 
- The workflow activity definitions are found in the [`Activities` directory](https://github.com/dapr/dotnet-sdk/tree/master/examples/Workflow/WorkflowConsoleApp/Activities).

## Prerequisites

- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) or [.NET 9](https://dotnet.microsoft.com/download/dotnet/9.0) installed

## Set up the environment

Clone the [.NET SDK repo](https://github.com/dapr/dotnet-sdk).

```sh
git clone https://github.com/dapr/dotnet-sdk.git
```

From the .NET SDK root directory, navigate to the Dapr Workflow example.

```sh
cd examples/Workflow
```

## Run the application locally

To run the Dapr application, you need to start the .NET program and a Dapr sidecar. Navigate to the `WorkflowConsoleApp` directory.

```sh
cd WorkflowConsoleApp
```

Start the program.

```sh
dotnet run
```

In a new terminal, navigate again to the `WorkflowConsoleApp` directory and run the Dapr sidecar alongside the program.

```sh
dapr run --app-id wfapp --dapr-grpc-port 4001 --dapr-http-port 3500
```

> Dapr listens for HTTP requests at `http://localhost:3500` and internal workflow gRPC requests at `http://localhost:4001`.

## Start a workflow

To start a workflow, you have two options:

1. Follow the directions from the console prompts.
1. Use the workflow API and send a request to Dapr directly. 

This guide focuses on the workflow API option. 

{{% alert title="Note" color="primary" %}}
  - You can find the commands below in the `WorkflowConsoleApp`/`demo.http` file.
  - The body of the curl request is the purchase order information used as the input of the workflow. 
  - The "12345678" in the commands represents the unique identifier for the workflow and can be replaced with any identifier of your choosing.
{{% /alert %}}


Run the following command to start a workflow. 

{{< tabs "Linux/MacOS" "Windows">}}

{{% codetab %}}

```bash
curl -i -X POST http://localhost:3500/v1.0/workflows/dapr/OrderProcessingWorkflow/start?instanceID=12345678 \
  -H "Content-Type: application/json" \
  -d '{"Name": "Paperclips", "TotalCost": 99.95, "Quantity": 1}'
```

{{% /codetab %}}

{{% codetab %}}

```powershell
curl -i -X POST http://localhost:3500/v1.0/workflows/dapr/OrderProcessingWorkflow/start?instanceID=12345678 `
  -H "Content-Type: application/json" `
  -d '{"Name": "Paperclips", "TotalCost": 99.95, "Quantity": 1}'
```

{{% /codetab %}}

{{< /tabs >}}

If successful, you should see a response like the following: 

```json
{"instanceID":"12345678"}
```

Send an HTTP request to get the status of the workflow that was started:

```bash
curl -i -X GET http://localhost:3500/v1.0/workflows/dapr/12345678
```

The workflow is designed to take several seconds to complete. If the workflow hasn't completed when you issue the HTTP request, you'll see the following JSON response (formatted for readability) with workflow status as `RUNNING`:

```json
{
  "instanceID": "12345678",
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

Once the workflow has completed running, you should see the following output, indicating that it has reached the `COMPLETED` status:

```json
{
  "instanceID": "12345678",
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

When the workflow has completed, the stdout of the workflow app should look like:

```log
info: WorkflowConsoleApp.Activities.NotifyActivity[0]
      Received order 12345678 for Paperclips at $99.95
info: WorkflowConsoleApp.Activities.ReserveInventoryActivity[0]
      Reserving inventory: 12345678, Paperclips, 1
info: WorkflowConsoleApp.Activities.ProcessPaymentActivity[0]
      Processing payment: 12345678, 99.95, USD
info: WorkflowConsoleApp.Activities.NotifyActivity[0]
      Order 12345678 processed successfully!
```

If you have Zipkin configured for Dapr locally on your machine, then you can view the workflow trace spans in the Zipkin web UI (typically at http://localhost:9411/zipkin/).

## Demo

Watch this video [demonstrating .NET Workflow](https://youtu.be/BxiKpEmchgQ?t=2557):

<iframe width="560" height="315" src="https://www.youtube-nocookie.com/embed/BxiKpEmchgQ?start=2557" title="YouTube video player" frameborder="0" allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share" allowfullscreen></iframe>

## Next steps

- [Try the Dapr Workflow quickstart]({{< ref workflow-quickstart.md >}})
- [Learn more about Dapr Workflow]({{< ref workflow-overview.md >}})