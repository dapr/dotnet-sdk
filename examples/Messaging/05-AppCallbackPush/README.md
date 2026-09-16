# Example 05: gRPC AppCallback Push Subscriptions (`Dapr.Messaging`)

This example demonstrates **gRPC push subscriptions** (`DeliveryMode.Programmatic`) in `Dapr.Messaging`.

## Overview

In the gRPC push delivery mode (`DeliveryMode.Programmatic`), the Dapr sidecar initiates gRPC calls to the application's gRPC server implementing Dapr's `AppCallback` service:
- `ListTopicSubscriptions`: Dapr asks the application which topics it subscribes to.
- `OnTopicEvent`: Dapr pushes incoming topic events directly to the application.

### Comparison: Streaming Pull vs. gRPC Push

| Feature | `DeliveryMode.Streaming` (Default) | `DeliveryMode.Programmatic` (gRPC Push) |
|---|---|---|
| **Connection Direction** | App initiates outbound gRPC stream to Dapr | Dapr initiates inbound gRPC calls to App |
| **Inbound App Port** | None required (zero open ports) | App must listen on gRPC port (`--app-port`, `--app-protocol grpc`) |
| **Hosting Model** | Console, Worker Service, Minimal API | ASP.NET Core with gRPC server enabled |
| **Best For** | Background services, firewalled environments, modern default | Existing gRPC service meshes or sidecar push architectures |

## Writing a gRPC Push Topic Handler

```csharp
[DaprTopic("pubsub", "payments", Delivery = DeliveryMode.Programmatic)]
public class PaymentProcessingHandler : ITopicHandler<PaymentReceived>
{
    private readonly ILogger<PaymentProcessingHandler> logger;

    public PaymentProcessingHandler(ILogger<PaymentProcessingHandler> logger)
    {
        this.logger = logger;
    }

    public Task<TopicResponseAction> HandleAsync(PaymentReceived payment, TopicContext context, CancellationToken ct)
    {
        if (payment.Amount <= 0)
        {
            return Task.FromResult(TopicResponseAction.Drop);
        }

        return Task.FromResult(TopicResponseAction.Success);
    }
}
```

## Configuring `Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

// Discovers handlers and registers gRPC AppCallbackService
builder.Services.AddDaprMessaging();

var app = builder.Build();

// Maps gRPC AppCallbackService endpoints
app.MapDaprMessaging();

app.Run();
```

## Running the Example

Run with `--app-protocol grpc` and specify the application's gRPC port:

```bash
dapr run --app-id appcallback-example \
         --app-port 5000 \
         --app-protocol grpc \
         --dapr-grpc-port 50001 \
         -- dotnet run --project examples/Messaging/05-AppCallbackPush/AppCallback.Example05.csproj
```

## Testing

```bash
dotnet test examples/Messaging/05-AppCallbackPush/AppCallback.Example05.Tests/AppCallback.Example05.Tests.csproj
```
