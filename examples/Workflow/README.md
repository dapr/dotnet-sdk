# Dapr Workflow with ASP.NET Core sample

This Dapr workflow example shows how to create a Dapr workflow (`Workflow`) and invoke it using the console.

## Prerequisites

- [.NET 6+](https://dotnet.microsoft.com/download) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://github.com/dapr/dotnet-sdk/)


## Optional Setup
Dapr workflow, as well as this example program, now support authentication through the use of API tokens. For more information on this, view the following document: [API Token](https://github.com/dapr/dotnet-sdk/blob/master/docs/api-tokens.md)

## Console App
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

## Task Chaining
Details can be found in Dapr [Workflow Patterns Task Chaining](https://docs.dapr.io/developing-applications/building-blocks/workflow/workflow-patterns/#task-chaining).

To run this sample, in the first terminal window run the following command from the WorkflowTaskChaining directory
```
dotnet tun
```
Next, in a separate terminal window, start the dapr sidecar:
```
dapr run --app-id wfapp --dapr-grpc-port 4001 --dapr-http-port 3500
```
The stdout of the workflow app should look like the following:
```
Workflow Started.
Step 1: Received input: 42.
Step 2: Received input: 43.
Step 3: Received input: 86.
Workflow state: Completed
Workflow result: 43 86 84
```

## Fan-Out/Fan-In
Details can be found in Dapr [Workflow Patterns Fan-Out/Fan-In](https://docs.dapr.io/developing-applications/building-blocks/workflow/workflow-patterns/#fan-outfan-in).

To run this sample, in the first terminal window run the following command from the WorkflowFanOutFanin directory
```
dotnet tun
```
Next, in a separate terminal window, start the dapr sidecar:
```
dapr run --app-id wfapp --dapr-grpc-port 4001 --dapr-http-port 3500
```
The stdout of the workflow app should look like the following:
```
Workflow Started.
calling task 3 ...  
calling task 2 ...
calling task 1 ...
Workflow state: Completed
```
The order of tasks log is uncertain because they are executed concurrently.

## Monitor
Details can be found in Dapr [Workflow Patterns Monitor](https://docs.dapr.io/developing-applications/building-blocks/workflow/workflow-patterns/#monitor).

To run this sample, in the first terminal window run the following command from the WorkflowMonitor directory
```
dotnet tun
```
Next, in a separate terminal window, start the dapr sidecar:
```
dapr run --app-id wfapp --dapr-grpc-port 4001 --dapr-http-port 3500
```
The stdout of the workflow app should look like the following:
```
Workflow Started.
...
Status is unhealthy. Set check interval to 1s
This job is healthy
This job is unhealthy
Status is unhealthy. Set check interval to 1s
Status is unhealthy. Set check interval to 1s
...
```

## External System Interaction
Details can be found in Dapr [Workflow Patterns External System Interaction](https://docs.dapr.io/developing-applications/building-blocks/workflow/workflow-patterns/#external-system-interaction).

To run this sample, in the first terminal window run the following command from the WorkflowMonitor directory
```
dotnet tun
```
Next, in a separate terminal window, start the dapr sidecar:
```
dapr run --app-id wfapp --dapr-grpc-port 4001 --dapr-http-port 3500
```
The stdout of the workflow app should look like the following:
```
// If press enter
Workflow Started.
Press [ENTER] in 10s to approve this workflow.
Approved.
Workflow demo-workflow-994ed458 is approved.
Running Approval activity ...
Approve Activity finished
Workflow state: Completed

// if do nothing
Workflow Started.
Press [ENTER] in 10s to approve this workflow.
Rejected.
Approval timeout.
Workflow demo-workflow-c9036657 is rejected.
Running Reject activity ...
Approval timeout.
Reject Activity finished
Workflow state: Completed
```

## Sub-Workflow
The sub-workflow pattern allows you to call a workflow from another workflow.
The `DemoWorkflow` class defines the workflow. It calls a sub-workflow `DemoSubWorkflow` to do the work. See the code snippet below:
```c#
    public class DemoWorkflow : Workflow<string, bool>
    {
        public override async Task<bool> RunAsync(WorkflowContext context, string instanceId)
        {
            Console.WriteLine($"Workflow {instanceId} Started.");
            string subInstanceId = instanceId + "-sub";
            ChildWorkflowTaskOptions options = new ChildWorkflowTaskOptions(subInstanceId);
            await context.CallChildWorkflowAsync<bool>(nameof(DemoSubWorkflow), "Hello, sub-workflow", options);
            return true;
        }
    }
```

The `DemoSubWorkflow` class defines the sub-workflow. It prints its instanceID and input, and then pauses for 5 seconds to simulate transaction processing. See the code snippet below:
```c#
    public class DemoSubWorkflow : Workflow<string, bool>
    {
        public override async Task<bool> RunAsync(WorkflowContext context, string input)
        {
            Console.WriteLine($"Workflow {context.InstanceId} Started.");
            Console.WriteLine($"Received input: {input}.");
            await context.CreateTimer(TimeSpan.FromSeconds(5));
            return true;
        }
    }
```
To run this sample, in the first terminal window run the following command from the WorkflowMonitor directory
```
dotnet tun
```
Next, in a separate terminal window, start the dapr sidecar:
```
dapr run --app-id wfapp --dapr-grpc-port 4001 --dapr-http-port 3500
```
The stdout of the workflow app should look like the following:
```
Workflow Started.
Workflow demo-workflow-ee513152 Started.
Workflow demo-workflow-ee513152-sub Started.
Received input: Hello, sub-workflow.
Workflow demo-workflow-ee513152 state: Completed
```