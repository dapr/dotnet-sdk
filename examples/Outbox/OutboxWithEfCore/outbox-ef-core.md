# Outbox pattern with EF Core

`Dapr.EntityFrameworkCore.Outbox` adds transactional outbox support on top of Entity
Framework Core so a service can atomically persist domain state **and** enqueue Dapr
pub/sub events. A background dispatcher publishes the enqueued events to a Dapr sidecar,
which wraps each payload in a CloudEvent and forwards it to the configured pub/sub
component.

Because payloads are stored as raw JSON, Dapr applies the CloudEvent envelope at publish
time — the SDK does **not** wrap events itself. This preserves standard Dapr subscriber
behaviour and lets subscribers written in any language consume events unchanged.

## When to use it

Use the outbox when losing an event on failure is worse than sending a duplicate. The
dispatcher provides **at-least-once** delivery: a successful save always leads to at
least one publish attempt; publish failures retry with exponential backoff up to
`MaxAttempts`; retries beyond that limit dead-letter the row for operator triage.

Subscribers should be idempotent. Every published message carries a stable
`cloudevent.id` equal to the outbox row `Id` (a `Guid`), so subscribers can dedupe on
that value.

## Concepts

| Concept                            | Purpose                                                                 |
| ---------------------------------- | ----------------------------------------------------------------------- |
| `IHasDomainEvents`                 | Marker on aggregates that expose domain events + `ClearDomainEvents`.   |
| `[DaprOutboxEvent]`                | Attribute on event types declaring the target pub/sub + topic.          |
| `IOutboxMessageFactory`            | Serialises a domain event into an `OutboxMessage` (default: attribute). |
| `DaprOutboxSaveChangesInterceptor` | EF interceptor that flushes buffered events into the outbox table.      |
| `IOutboxPendingBuffer`             | Per-`DbContext` buffer of events awaiting transactional persistence.    |
| `IOutboxClaimStrategy`             | Locks a batch of rows for a dispatcher instance.                        |
| `IOutboxDispatcher`                | Publishes claimed rows through `DaprClient`.                            |
| `DaprOutboxHostedService<T>`       | Long-running poll loop that invokes the dispatcher.                     |
| `OutboxRetentionHostedService<T>`  | Optional sweep that deletes processed rows past their retention window. |
| `DaprOutboxHealthCheck<T>`         | Reports Healthy/Degraded/Unhealthy based on oldest-unprocessed age.     |

## Getting started

Add the package:

```xml
<PackageReference Include="Dapr.EntityFrameworkCore.Outbox" />
```

Mark aggregates and events:

```csharp
public sealed class Order : IHasDomainEvents
{
    private readonly List<object> events = new();
    public IReadOnlyCollection<object> DomainEvents => events;
    public void ClearDomainEvents() => events.Clear();
    // ...raise events by adding to `events`
}

[DaprOutboxEvent("pubsub", "orders", CloudEventType = "com.example.order.placed")]
public sealed record OrderPlaced(Guid OrderId, string CustomerName, decimal Total);
```

Add the outbox to your model and DI:

```csharp
public sealed class OrdersDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(/* ... */);
        modelBuilder.AddDaprOutbox();
    }
}

builder.Services.AddDbContext<OrdersDbContext>((sp, options) =>
{
    options.UseSqlServer(connectionString);
    options.AddInterceptors(sp.GetRequiredService<DaprOutboxSaveChangesInterceptor>());
});

builder.Services.AddDaprClient();
builder.Services
    .AddDaprOutbox<OrdersDbContext>(o =>
    {
        o.PollInterval = TimeSpan.FromSeconds(2);
        o.HealthCheckThreshold = TimeSpan.FromMinutes(1);
        o.RetentionPeriod = TimeSpan.FromDays(14);
    })
    .AddDefaultDispatcher()
    .AddSqlServerClaimStrategy()        // or .AddPostgreSqlClaimStrategy()
    .AddRetentionService()
    .AddOutboxHealthCheck();
```

A `POST /orders` handler simply saves the aggregate — the interceptor writes the outbox
row inside the same transaction, and the dispatcher forwards it seconds later:

```csharp
app.MapPost("/orders", async (CreateOrderRequest req, OrdersDbContext db) =>
{
    var order = Order.Create(req.CustomerName, req.Total);
    db.Orders.Add(order);
    await db.SaveChangesAsync();
    return Results.Created($"/orders/{order.Id}", order);
});
```

## Provider notes

- **SQL Server**: use `AddSqlServerClaimStrategy()`. Claims run
  `UPDATE ... WITH (ROWLOCK, UPDLOCK, READPAST) OUTPUT INSERTED.*` for concurrent-safe
  non-blocking dispatch.
- **PostgreSQL**: use `AddPostgreSqlClaimStrategy()`. Claims run
  `SELECT ... FOR UPDATE SKIP LOCKED` inside a serializable transaction.
- **SQLite** (and any other provider): the default `RelationalOutboxClaimStrategy` works,
  but its EF LINQ query needs `DaprOutboxOptions.UseCompactDateTimeStorage = true` because
  the SQLite provider cannot translate `DateTimeOffset` comparisons. Pass the option to
  `AddDaprOutbox` in `OnModelCreating`; leave it `false` on SQL Server / PostgreSQL to
  preserve the native `datetimeoffset` column type.

## Delivery guarantees

- **At-least-once**: the outbox row is inserted in the same transaction as the aggregate
  change, so nothing is ever lost. Publish attempts retry with exponential backoff
  (capped at 5 minutes) up to `MaxAttempts`; beyond that, the row is dead-lettered
  (`LastError` set, `LockedUntil = null`, `AttemptCount = MaxAttempts`).
- **Idempotency**: the sidecar exposes `cloudevent.id = OutboxMessage.Id`. Subscribers
  should track processed IDs or design side effects to be idempotent.
- **Ordering**: dispatch order is *by `OccurredAt` ascending* within a batch, but no
  cross-batch or cross-instance ordering is guaranteed. If your topic requires
  per-partition ordering, use a broker-level partitioning key in the message metadata.

## Diagnostics

- **Structured logs** via `LoggerMessage.Define` — search for `Dapr.EntityFrameworkCore.Outbox`.
- **Tracing** via `ActivitySource("Dapr.EntityFrameworkCore.Outbox")`. Attach an OTLP
  exporter to see spans for interceptor flush, batch claim, and per-message publish.
- **Correlation**: any non-null `OutboxMessage.CorrelationId` is forwarded as the
  `traceparent` metadata entry so downstream subscribers can join the trace.
- **Health check**: `AddOutboxHealthCheck()` exposes the outbox age at `/health` under
  the name `dapr-outbox` (customisable). Below half `HealthCheckThreshold` → Healthy,
  between half and full → Degraded, at or above → Unhealthy.

## AOT / trimming

Provide a `JsonSerializerContext`-derived resolver via
`DaprOutboxOptions.JsonTypeInfoResolver`. When set, the default
`AttributeOutboxMessageFactory` uses it and avoids reflection-based JSON. If left
`null`, reflection is used and IL2026/IL3050 warnings are suppressed at the
factory boundary.

## Bulk publish

The v1 dispatcher publishes one message per outbox row. `DaprClient.BulkPublishEventAsync`
does not currently expose per-entry metadata for `byte[]` payloads, so we cannot preserve
unique `cloudevent.id`s in a single bulk call. A follow-up will add a
`BulkPublishByteEventAsync` overload on `DaprClient` and teach the dispatcher to prefer it
when available.
