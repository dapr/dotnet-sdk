---
type: docs
title: "Dapr .NET SDK Development"
linkTitle: "Development"
weight: 1000
description: Learn about local development options for .NET Dapr applications
no_list: true
---

## Thinking more than one at a time

Using your favorite IDE or editor to launch an application typically assumes that you only need to run one thing - the application you are debugging. However, developing microservices challenges you think about your local development process for *more than one at a time*. A microservices application has multiple services that you might need running at the same time as well as dependencies like state stores to manage.

Adding Dapr to your development process means you need to manage the following concerns:

- Each service you want to run
- A Dapr sidecar for each service
- Dapr component and configuration manifests 
- Additional dependencies such as state stores
- optional: the Dapr placement service for actors

This document will assume that you're building a production application, and want to create a repeatable and robust set of development practices. The guidance here is general, and applies to any .NET server application using Dapr (including actors).

## Managing components

You have two primary methods of storing component definitions for local development with Dapr:

- Use the default location (`~/.dapr/components`)
- Use your own location 

Creating a folder within your source code repository to store components and configuration will give you a way to version and share these definitions. The rest of this guide will assume you created a folder next to the application source code to store these files.

## Development options

The following setions will outline some tools and strategies you can use to run in local development, from lowest investment to highest. The guidance here will be .NET specific.

### Dapr CLI

*Consider this to be a .NET companion to the [Dapr Self-Hosted with Docker Guide]({{ ref self-hosted-overview.md }}))*.

The Dapr CLI provides you with a good base to work from by initializing a local redis container, zipkin container, the placement service, and component manifests for redis. This will enable you to work with the following building blocks on a fresh install with no additional setup:

- Service invocation
- Actors
- State Store
- Pub/Sub

You can run .NET services with `dapr run` as your strategy for developing locally. Plan on running one of these commands per-service in order to launch your application.

**Pro:** this is easy to set up since its part of the default Dapr installation

**Con:** this uses long-running docker containers on your machine, which might not be desirable

**Con:** the scalability of this approach is poor since it requires running a separate command per-service

---

For each service you need to choose:

- A unique app-id for addressing (`app-id`)
- A unique listening port for HTTP (`port`)

You also should have decided on where you are storing components (`components-path`).

The following command can be run from multiple terminals to launch each service, with the respective values substituted.

```sh
dapr run --app-id <app-id> --app-port <port> --components-path <components-path> -- dotnet run -p <project> --urls http://localhost:<port>
```

**Explanation:** this command will use `dapr run` to launch each service and its sidecar. The first half of the command (before `--`) passes required configuration to the Dapr CLI. The second half of the command (after `--`) passes required configuration to the `dotnet run` command.

> 💡 since you need to configure a unique port for each service, you can use this command to pass that port value to **both** Dapr and the service. `--urls http://localhost:<port>` will configure ASP.NET Core to listen for traffic on the provided port. Using configuration at the commandline is a more flexible approach than hardcoding a listening port elsewhere.

If any of your services do not accept HTTP traffic, then modify the command above by removing the `--app-port` and `--urls` arguments.

---

If you need to debug, then use the attach feature of your debugger to attach to one of the running processes.

If you want to scale up this approach, then consider building a script which automates this process for your whole application.

### Tye

[.NET Tye](https://github.com/dotnet/tye/) is a microservices development tool designed to make running many .NET services easy. Tye enables you to store a configuration of multiple .NET services, processes, and container images as a runnable application. 

Tye is advantageous for a .NET Dapr developer because:

- Tye has the ability to automate the dapr CLI built-in
- Tye understands .NET's conventions and requires almost no configuration for .NET services
- Tye can manage the lifetime of your dependencies in containers

**Pro:** Tye can automate all of the steps described above. You no longer need to think about concepts like ports or app-ids.

**Pro:** Since Tye can also manage containers for you, you can make those part of the application definition and stop the long-running containers on your machine.

--- 

Follow the [Tye Getting Started](https://github.com/dotnet/tye/blob/master/docs/getting_started.md) to install the `tye` CLI and create a `tye.yaml` for your application.

Next follow the steps in the [Tye Dapr recipe](https://github.com/dotnet/tye/blob/master/docs/recipes/dapr.md) to add Dapr. Make sure to specify the relative path to your components folder with `components-path` in `tye.yaml`.

Next add any additional container dependencies and add component definitions to the folder you created earlier.

You should end up with something like this:

```yaml
name: store-application
extensions:

  # Configuration for dapr goes here.
- name: dapr
  components-path: <components-path> 

# Services to run go here.
services:
  
  # The name will be used as the app-id. For a .NET project, Tye only needs the path to the project file.
- name: orders
  project: orders/orders.csproj
- name: products
  project: products/products.csproj
- name: store
  project: store/store.csproj

  # Containers you want to run need an image name and set of ports to expose.
- name: redis
  image: redis
  bindings:
    - port: 6973
```

Checkin `tye.yaml` in source control wiht the application code. 

You can now use `tye run` to launch the whole application from one terminal. When running, Tye has a dashboard at `http://localhost:8000` to view application status and logs.

---

Tye runs your services locally as normal .NET process. If you need to debug, then use the attach feature of your debugger to attach to one of the running processes. Since Tye is .NET aware, it has the ability to [start a process suspended](https://github.com/dotnet/tye/blob/master/docs/reference/commandline/tye-run.md#options) for startup debugging.

Tye also has an [option](https://github.com/dotnet/tye/blob/master/docs/reference/commandline/tye-run.md#options) to run your services in containers if you wish to test locally in containers.


### Docker-Compose

*Consider this to be a .NET companion to the [Dapr Self-Hosted with Docker Guide]({{ ref self-hosted-with-docker.md }}))*.

`docker-compose` is a CLI tool included with Docker Desktop that you can use to run multiple containers at a time.

`docker-compose` is a way to automate the lifecycle of multiple containers together, and offers a development experience similar to a production environment for applications targeting Kubernetes.

**Pro:** Since `docker-compose` manages containers for you, you can make dependencies part of the application definition and stop the long-running containers on your machine.

**Con:** most investment required, services need to be containerized to get started.

**Con:** can be difficult to debug and troubleshoot if you are unfamilar with Docker.

---

From the .NET perspective, there is no specialized guidance needed for `docker-compose` with Dapr. `docker-compose` runs containers, and once your service is in a container, configuring it similar to any other programming technology.

> 💡 in a container, an ASP.NET Core app will listen on port 80 by default. Remember this for when you need to configure the `--app-port` later.

To summarize the approach:

- Create a `Dockerfile` for each service
- Create a `docker-compose.yaml` and place check it in to the source code repository

To understand the authoring the `docker-compose.yaml` you should start with the [Hello, docker-compose sample](https://github.com/dapr/samples/tree/master/hello-docker-compose).

Similar to running locally with `dapr run` for each service you need to choose a unique app-id. Choosing the container name as the app-id will make this simple to remember.

The compose file will contain at a minimum:

- A network that the containers use to communiate
- Each service's container
- A `<service>-daprd` sidecar container with the service's port and app-id specified
- Additional dependencies that run in containers (redis for example)
- optional: Dapr placement container (for actors)

You can also view a larger example from the [eShopOnContainers](https://github.com/dotnet-architecture/eShopOnDapr/blob/master/docker-compose.yml) sample application.