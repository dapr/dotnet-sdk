---
type: docs
title: "Dapr .NET SDK Development with Docker-Compose"
linkTitle: "Docker Compose"
weight: 50000
description: Learn about local development with Docker-Compose
---

## Docker-Compose

*Consider this to be a .NET companion to the [Dapr Self-Hosted with Docker Guide]({{< ref self-hosted-with-docker.md >}})*.

`docker-compose` is a CLI tool included with Docker Desktop that you can use to run multiple containers at a time. It is a way to automate the lifecycle of multiple containers together, and offers a development experience similar to a production environment for applications targeting Kubernetes.

- **Pro:** Since `docker-compose` manages containers for you, you can make dependencies part of the application definition and stop the long-running containers on your machine.
- **Con:** most investment required, services need to be containerized to get started.
- **Con:** can be difficult to debug and troubleshoot if you are unfamilar with Docker.

### Using docker-compose

From the .NET perspective, there is no specialized guidance needed for `docker-compose` with Dapr. `docker-compose` runs containers, and once your service is in a container, configuring it similar to any other programming technology.

{{% alert title="💡 App Port" color="primary" %}}
In a container, an ASP.NET Core app will listen on port 80 by default. Remember this for when you need to configure the `--app-port` later.
{{% /alert %}}

To summarize the approach:

- Create a `Dockerfile` for each service
- Create a `docker-compose.yaml` and place check it in to the source code repository

To understand the authoring the `docker-compose.yaml` you should start with the [Hello, docker-compose sample](https://github.com/dapr/samples/tree/master/hello-docker-compose).

Similar to running locally with `dapr run` for each service you need to choose a unique app-id. Choosing the container name as the app-id will make this simple to remember.

The compose file will contain at a minimum:

- A network that the containers use to communicate
- Each service's container
- A `<service>-daprd` sidecar container with the service's port and app-id specified
- Additional dependencies that run in containers (redis for example)
- optional: Dapr placement container (for actors)

You can also view a larger example from the [eShopOnContainers](https://github.com/dotnet-architecture/eShopOnDapr/blob/master/docker-compose.yml) sample application.
