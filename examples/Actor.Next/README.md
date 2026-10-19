# Dapr.Actors.Next Examples

These are the worked examples for the six-part Dapr.Actors.Next tutorial.

- [01-Cart](01-Cart/) - A modern cart actor with generated registration, constructor injection, cached state, and an activation-local abandon-cart timer. Tutorial: [Part 1](../../docs/dotnet-actorsnext/tutorial/part-1.md).
- [02-Migration](02-Migration/) - Cart state schema migration with typed upcasters and unit-tested legacy data. Tutorial: [Part 2](../../docs/dotnet-actorsnext/tutorial/part-2.md).
- [03-PubSub](03-PubSub/) - Dynamic pub/sub delivery into actors with `[Subscribe]` and retry-gated acknowledgement. Tutorial: [Part 3](../../docs/dotnet-actorsnext/tutorial/part-3.md).
- [04-Auction](04-Auction/) - A state-machine auction actor with deterministic soft-close race testing. Tutorial: [Part 4](../../docs/dotnet-actorsnext/tutorial/part-4.md).
- [05-Interpreted](05-Interpreted/) - Runtime-defined smart-lock state machines verified before rollout and hosted by the interpreted actor. Tutorial: [Part 5](../../docs/dotnet-actorsnext/tutorial/part-5.md).
- [06-Approvals](06-Approvals/) - Interpreted approval-document machines onboarded at runtime, composed with a settlement workflow that retries and compensates. Tutorial: [Part 6](../../docs/dotnet-actorsnext/tutorial/part-6.md).
- [07-PerTypeOptions](07-PerTypeOptions/) - Two actor types with different lifecycle profiles: per-type idle timeout and reentrancy overrides that merge over the app-wide defaults.

Prerequisites: Dapr runtime 1.18+ for integration-style actor callback stream behavior, and the .NET 10 or .NET 11 SDK. The unit tests in these examples run with no sidecar, no state store, and no Docker.

The throughline is that each example pairs the authoring model with its test. The tests progressively need less infrastructure and reach further: no sidecar, then without a database, then without a broker, then a deterministic race, then verification as a deploy gate.

For remote-call simulation, paused state commits, virtual timer races, and callback-stream
fault injection, see [Deterministic actor concurrency tests](../../src/Dapr.Actors.Next.Testing/README.md).

Generated actor modules compose across loaded assemblies: inject `IActorProxyFactory` and `IActorRegistry`
to access their combined contracts and metadata. State migration families from those modules are combined
as well. Load the actor assemblies before calling `AddDaprActors` so their module registrations are available.
Nested generic return types and `ActorId` payloads use the same generated invocation path as other DTOs;
actors and interfaces may share simple CLR names in different namespaces, while Dapr actor type names
must remain unique.

Inherited actor methods with equivalent signatures produce a single proxy method and
wire operation. C# may require casting a merged interface to one of its base interfaces
when calling an ambiguously inherited method. Overloads, conflicting return types, or
incompatible multi-argument payload names sharing a wire name are rejected with
`DAPRGEN001`, including collisions across separate contracts implemented by one actor.
Give different operations distinct method names.

Actor saves commit all changed state names and deletions atomically. Use a state
component with transaction support (such as the Redis component used by the
integration tests). `ActorTestRuntime.Faults.FailNextStateTransaction()` fails the
entire pending batch, including delete-only resets, without changing durable
state. Dispose the test runtime with `await using`; it drains admitted work and
disposes each activation and its scoped services.

Cache eviction waits for an in-flight save before dropping entries, so the next read
cannot cache the pre-commit value. `EvictOnDirtyState = true` discards changes still
pending after that save; it does not cancel a transaction already submitted to the
store. Normal reads and replacements remain available while a save is waiting.
In-place changes to reference objects, including references held inside structs,
are detected at save time. Value types without references retain the scalar fast path.

SDK envelope metadata uses fixed camelCase JSON property names independently of the
payload's naming policy (including snake_case, kebab-case, and custom policies).
Previously written PascalCase, snake_case, and kebab-case metadata remains readable.
The `value` payload still uses the configured serializer options. Once the SDK magic
tag is recognized, malformed or missing envelope metadata/value raises
`ActorStateEnvelopeException` instead of falling back to raw state. Raw and legacy
`schemaVersion` envelopes remain supported.

Hierarchical state machines resolve each event at the nearest state declaring a
handler, `Ignore`, or `Defer`. A child's handler overrides a parent's ignore/defer;
a child's ignore/defer overrides an inherited handler. Deferred replay uses the
same resolution rules after transitions.
