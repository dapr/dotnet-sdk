# Dapr Workflow Retry Policy Example

This example demonstrates how to use **retry policies** with Dapr Workflows. Retry policies enable automatic retries of activities and child workflows that experience transient failures, improving the resilience of your workflow applications.

## What this example shows

1. **Simple fixed-interval retry** – An activity is retried up to 5 times with a constant 1-second delay between attempts.
2. **Exponential back-off retry** – An activity is retried with increasing delays (1s → 2s → 4s → ...) up to a configurable maximum interval.
3. **Child workflow retry** – A child (sub) workflow is retried using a retry policy when it throws an exception.

Each scenario uses a `FlakyActivity` or `FlakyChildWorkflow` that simulates transient failures by throwing exceptions for a configurable number of attempts before succeeding.

## Retry policy parameters

When creating a `WorkflowRetryPolicy`, you can configure:

| Parameter | Description | Default |
|---|---|---|
| `maxNumberOfAttempts` | Maximum number of attempts (including the first). Must be ≥ 1. | *(required)* |
| `firstRetryInterval` | Delay before the first retry. | *(required)* |
| `backoffCoefficient` | Multiplier applied to the delay after each retry (for exponential backoff). Must be ≥ 1.0. | `1.0` (fixed interval) |
| `maxRetryInterval` | Maximum delay between retries regardless of backoff. | 1 hour |
| `retryTimeout` | Overall timeout for all retry attempts. | Infinite |

### Example: fixed interval

```csharp
var options = new WorkflowTaskOptions(
    new WorkflowRetryPolicy(
        maxNumberOfAttempts: 5,
        firstRetryInterval: TimeSpan.FromSeconds(1)));

await context.CallActivityAsync<string>(nameof(MyActivity), input, options);
```

### Example: exponential back-off

```csharp
var options = new WorkflowTaskOptions(
    new WorkflowRetryPolicy(
        maxNumberOfAttempts: 10,
        firstRetryInterval: TimeSpan.FromSeconds(1),
        backoffCoefficient: 2.0,
        maxRetryInterval: TimeSpan.FromSeconds(30)));

await context.CallActivityAsync<string>(nameof(MyActivity), input, options);
```

### Example: child workflow with retry

```csharp
var options = new ChildWorkflowTaskOptions(
    RetryPolicy: new WorkflowRetryPolicy(
        maxNumberOfAttempts: 3,
        firstRetryInterval: TimeSpan.FromSeconds(2),
        backoffCoefficient: 1.5,
        maxRetryInterval: TimeSpan.FromSeconds(10)));

await context.CallChildWorkflowAsync<string>(nameof(MyChildWorkflow), input, options);
```

## Prerequisites

- [.NET 8+](https://dotnet.microsoft.com/download) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://github.com/dapr/dotnet-sdk/)

## Running the example

In one terminal window, start the Dapr sidecar:

```sh
dapr run --app-id wfretry --dapr-grpc-port 50001 --dapr-http-port 3500
```

In a separate terminal window, from the `WorkflowRetryPolicy` directory, run:

```sh
dotnet run
```

### Expected output

You should see log messages showing the transient failures and retries, followed by successful completion:

```
Starting workflow 'retry-demo-xxxxxxxx'...

[SimpleRetry] Attempt 1 of max 3 before success (instance: retry-demo-xxxxxxxx)
[SimpleRetry] Attempt 2 of max 3 before success (instance: retry-demo-xxxxxxxx)
[SimpleRetry] Attempt 3 of max 3 before success (instance: retry-demo-xxxxxxxx)
[SimpleRetry] Succeeded on attempt 3!
Result: SimpleRetry completed after 3 attempt(s)

[ExponentialBackoff] Attempt 1 of max 4 before success (instance: retry-demo-xxxxxxxx)
[ExponentialBackoff] Attempt 2 of max 4 before success (instance: retry-demo-xxxxxxxx)
[ExponentialBackoff] Attempt 3 of max 4 before success (instance: retry-demo-xxxxxxxx)
[ExponentialBackoff] Attempt 4 of max 4 before success (instance: retry-demo-xxxxxxxx)
[ExponentialBackoff] Succeeded on attempt 4!
Result: ExponentialBackoff completed after 4 attempt(s)

[FlakyChildWorkflow] Invocation #1 (instance: ...)
[FlakyChildWorkflow] Invocation #2 (instance: ...)
[FlakyChildWorkflow] FlakyChildWorkflow completed with input 'Hello from retry demo' on invocation #2
Result: FlakyChildWorkflow completed with input 'Hello from retry demo' on invocation #2

Workflow completed successfully! Results:
  - SimpleRetry completed after 3 attempt(s)
  - ExponentialBackoff completed after 4 attempt(s)
  - FlakyChildWorkflow completed with input 'Hello from retry demo' on invocation #2
```

## Learn more

- [Dapr Workflow retry policies documentation](https://docs.dapr.io/developing-applications/building-blocks/workflow/workflow-features-concepts/#retry-policies)
- [Dapr Workflow .NET SDK](https://docs.dapr.io/developing-applications/sdks/dotnet/dotnet-workflow/)
