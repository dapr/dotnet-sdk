---
type: docs
title: "Dapr .NET SDK Development with Dapr CLI"
linkTitle: "Dapr CLI"
weight: 30000
description: Learn about local development with the Dapr CLI
---

## Dapr CLI

*Consider this to be a .NET companion to the [Dapr Self-Hosted with Docker Guide]({{< ref self-hosted-with-docker.md >}})*.

The Dapr CLI provides you with a good base to work from by initializing a local redis container, zipkin container, the placement service, and component manifests for redis. This will enable you to work with the following building blocks on a fresh install with no additional setup:

- [Service invocation]({{< ref service-invocation >}})
- [State Store]({{< ref state-management >}})
- [Pub/Sub]({{< ref pubsub >}})
- [Actors]({{< ref actors >}})

You can run .NET services with `dapr run` as your strategy for developing locally. Plan on running one of these commands per-service in order to launch your application.

- **Pro:** this is easy to set up since its part of the default Dapr installation
- **Con:** this uses long-running docker containers on your machine, which might not be desirable
- **Con:** the scalability of this approach is poor since it requires running a separate command per-service

### Using the Dapr CLI

For each service you need to choose:

- A unique app-id for addressing (`app-id`)
- A unique listening port for HTTP (`port`)

You also should have decided on where you are storing components (`components-path`).

The following command can be run from multiple terminals to launch each service, with the respective values substituted.

```sh
dapr run --app-id <app-id> --app-port <port> --components-path <components-path> -- dotnet run -p <project> --urls http://localhost:<port>
```

**Explanation:** this command will use `dapr run` to launch each service and its sidecar. The first half of the command (before `--`) passes required configuration to the Dapr CLI. The second half of the command (after `--`) passes required configuration to the `dotnet run` command.

{{% alert title="💡 Ports" color="primary" %}}
Since you need to configure a unique port for each service, you can use this command to pass that port value to **both** Dapr and the service. `--urls http://localhost:<port>` will configure ASP.NET Core to listen for traffic on the provided port. Using configuration at the commandline is a more flexible approach than hardcoding a listening port elsewhere.
{{% /alert %}}

If any of your services do not accept HTTP traffic, then modify the command above by removing the `--app-port` and `--urls` arguments.

### Next steps

If you need to debug, then use the attach feature of your debugger to attach to one of the running processes.

If you want to scale up this approach, then consider building a script which automates this process for your whole application.
