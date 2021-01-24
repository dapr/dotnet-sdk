---
type: docs
title: "Getting started with the Dapr client .NET SDK"
linkTitle: "Client"
weight: 10000
description: How to get up and running with the Dapr .NET SDK
---

The Dapr client package allows you to interact with other Dapr applications from a .NET application.

## Pre-requisites

- [Dapr CLI]({{< ref install-dapr-cli.md >}}) installed
- Initialized [Dapr environment]({{< ref install-dapr-selfhost.md >}})
- [Dotnet 5.0+](https://dotnet.microsoft.com/download) installed

## Building blocks

The .NET SDK allows you to interface with all of the [Dapr building blocks]({{< ref building-blocks >}}).

### Invoke a service

- For a full guide on service invocation visit [How-To: Invoke a service]({{< ref howto-invoke-discover-services.md >}}).
- Visit [.NET SDK samples](https://github.com/dapr/python-sdk/tree/daprdocs-setup/examples/invoke-simple) for code samples and instructions to try out service invocation

### Save & get application state

- For a full list of state operations visit [How-To: Get & save state]({{< ref howto-get-save-state.md >}}).
- Visit [Python SDK examples](https://github.com/dapr/python-sdk/tree/daprdocs-setup/examples/state_store) for code samples and instructions to try out state management

### Publish messages

- For a full list of state operations visit [How-To: Publish & subscribe]({{< ref howto-publish-subscribe.md >}}).
- Visit [Python SDK examples](https://github.com/dapr/python-sdk/tree/daprdocs-setup/examples/pubsub-simple) for code samples and instructions to try out pub/sub

### Interact with output bindings

- For a full guide on output bindings visit [How-To: Use bindings]({{< ref howto-bindings.md >}}).
- Visit [Python SDK examples](https://github.com/dapr/python-sdk/tree/daprdocs-setup/examples/invoke-binding) for code samples and instructions to try out output bindings

### Retrieve secrets

- For a full guide on secrets visit [How-To: Retrieve secrets]({{< ref howto-secrets.md >}}).
- Visit [Python SDK examples](https://github.com/dapr/python-sdk/tree/daprdocs-setup/examples/secret_store) for code samples and instructions to try out retrieving secrets

## Related links
- [Python SDK examples](https://github.com/dapr/python-sdk/tree/daprdocs-setup/examples)