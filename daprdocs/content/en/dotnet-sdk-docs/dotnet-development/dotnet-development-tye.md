---
type: docs
title: "Dapr .NET SDK Development with Project Tye"
linkTitle: "Project Tye"
weight: 40000
description: Learn about local development with Project Tye
---

## Project Tye

[.NET Project Tye](https://github.com/dotnet/tye/) is a microservices development tool designed to make running many .NET services easy. Tye enables you to store a configuration of multiple .NET services, processes, and container images as a runnable application. 

Tye is advantageous for a .NET Dapr developer because:

- Tye has the ability to automate the dapr CLI built-in
- Tye understands .NET's conventions and requires almost no configuration for .NET services
- Tye can manage the lifetime of your dependencies in containers

Pros/cons:
- **Pro:** Tye can automate all of the steps described above. You no longer need to think about concepts like ports or app-ids.
- **Pro:** Since Tye can also manage containers for you, you can make those part of the application definition and stop the long-running containers on your machine.

### Using Tye

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

Checkin `tye.yaml` in source control with the application code. 

You can now use `tye run` to launch the whole application from one terminal. When running, Tye has a dashboard at `http://localhost:8000` to view application status and logs.

### Next steps

Tye runs your services locally as normal .NET process. If you need to debug, then use the attach feature of your debugger to attach to one of the running processes. Since Tye is .NET aware, it has the ability to [start a process suspended](https://github.com/dotnet/tye/blob/master/docs/reference/commandline/tye-run.md#options) for startup debugging.

Tye also has an [option](https://github.com/dotnet/tye/blob/master/docs/reference/commandline/tye-run.md#options) to run your services in containers if you wish to test locally in containers.
