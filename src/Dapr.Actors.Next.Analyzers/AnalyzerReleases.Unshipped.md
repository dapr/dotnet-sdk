; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
DAPR1410 | Compatibility | Warning | Actor state shape change breaks shipped serialization
DAPR1411 | Concurrency | Warning | Actor turn must not escape the scheduler
DAPR1412 | Concurrency | Warning | Actor turn must not block
DAPR1413 | Determinism | Warning | Actor turn must use TimeProvider
DAPR1414 | Determinism | Warning | Actor turn must use a scheduler-aware seeded source
DAPR1415 | Compatibility | Warning | Actor state migration target is unreachable
DAPR1416 | Design | Info | Actor turn filter should stay cross-cutting
DAPR1417 | Usage | Warning | Actor interface method must return an asynchronous type
DAPR1418 | Compatibility | Warning | Actor interface change breaks shipped wire contract
DAPR1419 | Concurrency | Warning | Actor field should not hold mutable shared state
DAPR1420 | Usage | Warning | Actor type name must disambiguate shared actor contracts
DAPR1421 | Usage | Warning | Actor implementation must expose a generated client contract
DAPR1423 | Compatibility | Warning | Actor state type is not connected to its migration family
DAPR1424 | Compatibility | Warning | Actor state migration chain has a gap
DAPR1425 | Compatibility | Warning | Actor state migration step requires an upcaster
DAPR1426 | Compatibility | Warning | Actor state migration fold path is ambiguous
DAPR1427 | Usage | Warning | Actor state name maps to multiple migration families
DAPR1428 | Usage | Info | Actor state usage should target the latest state version
DAPR1429 | Usage | Error | Scheduled actor callback does not match a dispatchable actor method
DAPR1430 | Usage | Warning | Scheduled actor callback targets an unknown actor type
DAPR1431 | Usage | Error | Scheduled actor callback method is not exposed through a generated actor client
