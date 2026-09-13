; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
DAPR1610 | Dapr.Messaging.Analyzers | Error | Topic registered for both Streaming and Programmatic
DAPR1611 | Dapr.Messaging.Analyzers | Error | [DaprTopic] on a class not implementing ITopicHandler<T>
DAPR1612 | Dapr.Messaging.Analyzers | Warning | Message type not registered in a JsonSerializerContext
DAPR1613 | Dapr.Messaging.Analyzers | Warning | Programmatic subscription missing MapDaprAppCallback call
DAPR1615 | Dapr.Messaging.Analyzers | Warning | Endpoint mapping call has no matching subscribers
