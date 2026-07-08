# Dapr.Actors.Next Examples

These are the worked examples for the six-part Dapr.Actors.Next tutorial.

- [01-Cart](01-Cart/) - A modern cart actor with generated registration, constructor injection, cached state, and an abandon-cart reminder. Tutorial: [Part 1](../../docs/dotnet-actorsnext/tutorial/part-1.md).
- [02-Migration](02-Migration/) - Cart state schema migration with typed upcasters and unit-tested legacy data. Tutorial: [Part 2](../../docs/dotnet-actorsnext/tutorial/part-2.md).
- [03-PubSub](03-PubSub/) - Dynamic pub/sub delivery into actors with `[Subscribe]` and retry-gated acknowledgement. Tutorial: [Part 3](../../docs/dotnet-actorsnext/tutorial/part-3.md).
- [04-Auction](04-Auction/) - A state-machine auction actor with deterministic soft-close race testing. Tutorial: [Part 4](../../docs/dotnet-actorsnext/tutorial/part-4.md).
- [05-Interpreted](05-Interpreted/) - Runtime-defined smart-lock state machines verified before rollout and hosted by the interpreted actor. Tutorial: [Part 5](../../docs/dotnet-actorsnext/tutorial/part-5.md).
- [06-Approvals](06-Approvals/) - Interpreted approval-document machines onboarded at runtime, composed with a settlement workflow that retries and compensates. Tutorial: [Part 6](../../docs/dotnet-actorsnext/tutorial/part-6.md).

Prerequisites: Dapr runtime 1.18+ for integration-style actor callback stream behavior, and the .NET 10 SDK. The unit tests in these examples run with no sidecar, no state store, and no Docker.

The throughline is that each example pairs the authoring model with its test. The tests progressively need less infrastructure and reach further: no sidecar, then without a database, then without a broker, then a deterministic race, then verification as a deploy gate.
