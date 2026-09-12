; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
DAPR1601 | Dapr.Messaging.Generators | Error | [DaprTopic] on a class not implementing ITopicHandler<T>
DAPR1602 | Dapr.Messaging.Generators | Error | [DaprTopic] on a non-class type
DAPR1603 | Dapr.Messaging.Generators | Error | Duplicate (pubsub, topic, delivery) subscription
DAPR1605 | Dapr.Messaging.Generators | Error | CEL Match without a Priority
