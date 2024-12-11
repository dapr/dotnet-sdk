---
type: docs
title: "Dapr Messaging .NET SDK"
linkTitle: "Messaging"
weight: 60000
description: Get up and running with the Dapr Messaging .NET SDK
---

With the Dapr Messaging package, you can interact with the Dapr messaging APIs from a .NET application. In the
v1.15 release, this package only contains the functionality corresponding to the 
[streaming PubSub capability](https://docs.dapr.io/developing-applications/building-blocks/pubsub/howto-publish-subscribe/#subscribe-to-topics).

Future Dapr .NET SDK releases will migrate existing messaging capabilities out from Dapr.Client to this 
Dapr.Messaging package. This will be documented in the release notes, documentation and obsolete attributes in advance.

To get started, walk through the [Dapr Messaging]({{< ref dotnet-messaging-pubsub-howto.md >}}) how-to guide and
refer to [best practices documentation]({{< ref dotnet-messaging-pubsub-usage.md >}}) for additional guidance.