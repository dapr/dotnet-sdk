# Dapr Workflow with ASP.NET Core sample

This Dapr workflow example shows how to create a Dapr workflow (`Workflow`) and invoke it using ASP.NET Core web APIs.

## Prerequisites

- [.NET 6+](https://dotnet.microsoft.com/download) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://github.com/dapr/dotnet-sdk/)

## Projects in sample

This sample contains a single [WorkflowWebApp](./WorkflowWebApp) ASP.NET Core project. It combines both the workflow implementations and the web APIs for starting and querying workflows instances.

The main `Program.cs` file contains the main setup of the app, including the registration of the web APIs and the registration of the workflow and workflow activities. The workflow definition is found in `Workflows` directory and the workflow activity definitions are found in the `Activities` directory.

## Running the example

To run the workflow web app locally, run this command in the `WorkflowWebApp` directory:

```sh
dapr run --app-id wfwebapp dotnet run
```

The application will listen for HTTP requests at `http://localhost:10080`.

This workflow example utilizes a redis statestore. In order to populate items into the state store, an HTTP command must first be sent down to restock the inventory:

curl -i -X POST http://localhost:10080/reset

To start a workflow, use the following command to send an HTTP POST request, which triggers an HTTP API that starts the workflow using the Dapr Workflow client. Two identical `curl` commands are shown, one for Linux/macOS (bash) and the other for Windows (PowerShell). The body of the request is used as the input of the workflow.

On Linux/macOS (bash):

```bash
curl -i -X POST http://localhost:10080/orders \
  -H "Content-Type: application/json" \
  -d '{"name": "Paperclips", "totalCost": 99.95, "quantity": 1}'
```

On Windows (PowerShell):

```powershell
curl -i -X POST http://localhost:10080/orders `
  -H "Content-Type: application/json" `
  -d '{"name": "Paperclips", "totalCost": 99.95, "quantity": 1}'
```

If successful, you should see a response like the following, which contains a `Location` header pointing to a status endpoint for the workflow that was created with a randomly generated 8-digit ID:

```http
HTTP/1.1 202 Accepted
Content-Length: 0
Date: Tue, 24 Jan 2023 00:02:02 GMT
Server: Kestrel
Location: http://localhost:10080/orders/cdcce425
```

Next, send an HTTP request to the URL in the `Location` header in the previous HTTP response, like in the following example:

```bash
curl -i http://localhost:10080/orders/cdcce425
```

If the workflow has completed running, you should see the following output (formatted for readability):

```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Date: Tue, 24 Jan 2023 00:10:53 GMT
Server: Kestrel
Transfer-Encoding: chunked

{
    "details": {
        "name": "Paperclips",
        "quantity": 1,
        "totalCost": 99.95
    },
    "result": {
        "processed": true
    },
    "status": "Completed"
}
```

If the workflow hasn't completed yet, you might instead see the following:

```http
HTTP/1.1 202 Accepted
Content-Type: application/json; charset=utf-8
Date: Tue, 24 Jan 2023 00:17:49 GMT
Location: http://localhost:10080/orders/cdcce425
Server: Kestrel
Transfer-Encoding: chunked

{
    "details": {
        "name": "Paperclips",
        "quantity": 1,
        "totalCost": 99.95
    },
    "status": "Running"
}
```

When the workflow has completed, the stdout of the web app should look like the following:

```log
info: WorkflowWebApp.Activities.NotifyActivity[0]
      Received order cdcce425 for Paperclips at $99.95
info: WorkflowWebApp.Activities.ReserveInventoryActivity[0]
      Reserving inventory: cdcce425, Paperclips, 1
info: WorkflowWebApp.Activities.ProcessPaymentActivity[0]
      Processing payment: cdcce425, 99.95, USD
info: WorkflowWebApp.Activities.NotifyActivity[0]
      Order cdcce425 processed successfully!
```
