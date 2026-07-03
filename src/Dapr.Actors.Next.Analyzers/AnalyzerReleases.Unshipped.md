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
DAPR1415 | Compatibility | Warning | Actor state upcaster chain has a version gap
DAPR1416 | Design | Info | Actor turn filter should stay cross-cutting
DAPR1417 | Usage | Warning | Actor interface method must return an asynchronous type
DAPR1418 | Compatibility | Warning | Actor interface change breaks shipped wire contract
DAPR1419 | Concurrency | Warning | Actor field should not hold mutable shared state
