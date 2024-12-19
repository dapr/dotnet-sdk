---
type: docs
title: "DaprWorkflowClient usage"
linkTitle: "DaprWorkflowClient usage"
weight: 100000
description: Essential tips and advice for using DaprWorkflowClient
---

## Lifetime management

A `DaprWorkflowClient` holds access to networking resources in the form of TCP sockets used to communicate with the Dapr sidecar as well
as other types used in the management and operation of Workflows. `DaprWorkflowClient` implements `IAsyncDisposable` to support eager
cleanup of resources.

## Dependency Injection

The `AddDaprWorkflow()` method will register the Dapr workflow services with ASP.NET Core dependency injection. This method
requires an options delegate that defines each of the workflows and activities you wish to register and use in your application.

{{% alert title="Note" color="primary" %}} 

This method will attempt to register a `DaprClient` instance, but this will only work if it hasn't already been registered with another
lifetime. For example, an earlier call to `AddDaprClient()` with a singleton lifetime will always use a singleton regardless of the
lifetime chose for the workflow client. The `DaprClient` instance will be used to communicate with the Dapr sidecar and if it's not
yet registered, the lifetime provided during the `AddDaprWorkflow()` registration will be used to register the `DaprWorkflowClient`
as well as its own dependencies.

{{% /alert %}} 

### Singleton Registration
By default, the `AddDaprWorkflow` method will register the `DaprWorkflowClient` and associated services using a singleton lifetime. This means
that the services will be instantiated only a single time.

The following is an example of how registration of the `DaprWorkflowClient` as it would appear in a typical `Program.cs` file:

```csharp
builder.Services.AddDaprWorkflow(options => {
    options.RegisterWorkflow<YourWorkflow>();
    options.RegisterActivity<YourActivity>();
});

var app = builder.Build();
await app.RunAsync();
```

### Scoped Registration

While this may generally be acceptable in your use case, you may instead wish to override the lifetime specified. This is done by passing a `ServiceLifetime`
argument in `AddDaprWorkflow`. For example, you may wish to inject another scoped service into your ASP.NET Core processing pipeline
that needs context used by the `DaprClient` that wouldn't be available if the former service were registered as a singleton.

This is demonstrated in the following example:

```csharp
builder.Services.AddDaprWorkflow(options => {
    options.RegisterWorkflow<YourWorkflow>();
    options.RegisterActivity<YourActivity>();
}, ServiceLifecycle.Scoped);

var app = builder.Build();
await app.RunAsync();
```

### Transient Registration

Finally, Dapr services can also be registered using a transient lifetime meaning that they will be initialized every time they're injected. This
is demonstrated in the following example:

```csharp
builder.Services.AddDaprWorkflow(options => {
    options.RegisterWorkflow<YourWorkflow>();
    options.RegisterActivity<YourActivity>();
}, ServiceLifecycle.Transient);

var app = builder.Build();
await app.RunAsync();
```

## Injecting Services into Workflow Activities
Workflow activities support the same dependency injection that developers have come to expect of modern C# applications. Assuming a proper
registration at startup, any such type can be injected into the constructor of the workflow activity and available to utilize during
the execution of the workflow. This makes it simple to add logging via an injected `ILogger` or access to other Dapr 
building blocks by injecting `DaprClient` or `DaprJobsClient`, for example.

```csharp
internal sealed class SquareNumberActivity : WorkflowActivity<int, int>
{
    private readonly ILogger _logger;
    
    public MyActivity(ILogger logger)
    {
        this._logger = logger;
    }
    
    public override Task<int> RunAsync(WorkflowActivityContext context, int input) 
    {
        this._logger.LogInformation("Squaring the value {number}", input);
        var result = input * input;
        this._logger.LogInformation("Got a result of {squareResult}", result);
        
        return Task.FromResult(result);
    }
}
```

### Using ILogger in Workflow
Because workflows must be deterministic, it is not possible to inject arbitrary services into them. For example, 
if you were able to inject a standard `ILogger` into a workflow and it needed to be replayed because of an error,
subsequent replay from the event source log would result in the log recording additional operations that didn't actually
take place a second or third time because their results were sourced from the log. This has the potential to introduce 
a significant amount of confusion. Rather, a replay-safe logger is made available for use within workflows. It will only 
log events the first time the workflow runs and will not log anything whenever the workflow is being replaced.

This logger can be retrieved from a method present on the `WorkflowContext` available on your workflow instance and
otherwise used precisely as you might otherwise use an `ILogger` instance.

An end-to-end sample demonstrating this can be seen in the 
[.NET SDK repository](https://github.com/dapr/dotnet-sdk/blob/master/examples/Workflow/WorkflowConsoleApp/Workflows/OrderProcessingWorkflow.cs)
but a brief extraction of this sample is available below.

```csharp
public class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
{
    public override async Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
    {
        string orderId = context.InstanceId;
        var logger = context.CreateReplaySafeLogger<OrderProcessingWorkflow>(); //Use this method to access the logger instance

        logger.LogInformation("Received order {orderId} for {quantity} {name} at ${totalCost}", orderId, order.Quantity, order.Name, order.TotalCost);
        
        //...
    }
}
```


 