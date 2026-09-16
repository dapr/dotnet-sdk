; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
DAPR1610 | Dapr.Messaging.Analyzers | Warning | Topic registered for multiple delivery modes (Streaming and Programmatic)
DAPR1611 | Dapr.Messaging.Analyzers | Error | [DaprTopic] on a class not implementing ITopicHandler<T>
DAPR1612 | Dapr.Messaging.Analyzers | Warning | Message type not registered in a JsonSerializerContext
DAPR1613 | Dapr.Messaging.Analyzers | Warning | Programmatic subscription missing MapDaprAppCallback call
DAPR1614 | Dapr.Messaging.Analyzers | Warning | DaprMessagingRegistration called directly instead of AddDaprMessaging
DAPR1615 | Dapr.Messaging.Analyzers | Warning | Endpoint mapping call has no matching subscribers
DAPR1616 | Dapr.Messaging.Analyzers | Warning | [DaprTopic] opts into a feature without its companion properties
DAPR1617 | Dapr.Messaging.Analyzers | Warning | [DaprTopic] sets a property that is ignored for the selected feature or delivery mode
