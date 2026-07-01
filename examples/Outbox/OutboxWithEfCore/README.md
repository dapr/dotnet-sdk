# Outbox with EF Core example

A minimal ASP.NET Core Web API showing how to use `Dapr.EntityFrameworkCore.Outbox` with
SQLite to publish domain events transactionally through a Dapr sidecar.

Every `POST /orders` writes an `Orders` row **and** an `DaprOutboxMessages` row inside the
same EF Core transaction. A background hosted service polls the outbox table and forwards
each row to the Dapr pub/sub component as a CloudEvent on the `orders` topic.

## Prerequisites

- .NET 8 SDK or newer
- The Dapr CLI (`dapr init` completed)
- A pub/sub component named `pubsub`. `dapr init` provisions an in-memory one by default.

## Run

```bash
dapr run --app-id orders --app-port 5000 --dapr-http-port 3500 -- \
    dotnet run --project OutboxWithEfCore.csproj --urls http://localhost:5000
```

Create an order:

```bash
curl -X POST http://localhost:5000/orders \
     -H "Content-Type: application/json" \
     -d '{"customerName":"ada","totalAmount":42.00}'
```

Within a couple of seconds the outbox dispatcher publishes an `OrderPlaced` CloudEvent to
the `orders` topic. Any subscriber wired to that topic — for instance, another Dapr app
using `[Topic("pubsub", "orders")]` — will receive it. Watch `DaprOutboxMessages` in the
SQLite file (`orders.db`) to see `ProcessedAt` populate as messages drain.

## Health check

Hit `GET /health` to see the outbox health. When more than one minute of lag accumulates
the check reports `Unhealthy`.

## Notes

- SQLite needs `UseCompactDateTimeStorage = true` because its EF provider cannot translate
  `DateTimeOffset` comparisons. SQL Server and PostgreSQL should leave that option `false`.
- On SQL Server call `.AddSqlServerClaimStrategy()`; on PostgreSQL call
  `.AddPostgreSqlClaimStrategy()` for provider-native locking semantics.
