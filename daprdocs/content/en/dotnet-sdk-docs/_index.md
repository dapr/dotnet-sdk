---
type: docs
title: "Dapr .NET SDK"
linkTitle: ".NET"
weight: 1000
description: .NET SDK packages for developing Dapr applications
no_list: true
cascade:
  github_repo: https://github.com/dapr/dotnet-sdk
  github_subdir: daprdocs/content/en/dotnet-sdk-docs
  path_base_for_github_subdir: content/en/developing-applications/sdks/dotnet/
  github_branch: master
---

Dapr offers a variety of packages to help with the development of .NET applications. Using them you can create .NET clients, servers, and virtual actors with Dapr.

## Prerequisites

- [Dapr CLI]({{< ref install-dapr-cli.md >}}) installed
- Initialized [Dapr environment]({{< ref install-dapr-selfhost.md >}})
- [.NET 8](https://dotnet.microsoft.com/download) or [.NET 9](https://dotnet.microsoft.com/download) installed

## Installation

To get started with the Client .NET SDK, install the Dapr .NET SDK package:

```sh
dotnet add package Dapr.Client
```

## Try it out

Put the Dapr .NET SDK to the test. Walk through the .NET quickstarts and tutorials to see Dapr in action:

| SDK samples | Description |
| ----------- | ----------- |
| [Quickstarts]({{< ref quickstarts >}}) | Experience Dapr's API building blocks in just a few minutes using the .NET SDK. |
| [SDK samples](https://github.com/dapr/dotnet-sdk/tree/master/examples) | Clone the SDK repo to try out some examples and get started. |
| [Pub/sub tutorial](https://github.com/dapr/quickstarts/tree/master/tutorials/pub-sub) | See how Dapr .NET SDK works alongside other Dapr SDKs to enable pub/sub applications. |

## Available packages

<div class="card-deck">
  <div class="card">
    <div class="card-body">
      <h5 class="card-title"><b>Client</b></h5>
      <p class="card-text">Create .NET clients that interact with a Dapr sidecar and other Dapr applications.</p>
      <a href="{{< ref dotnet-client >}}" class="stretched-link"></a>
    </div>
  </div>
  <div class="card">
    <div class="card-body">
      <h5 class="card-title"><b>Server</b></h5>
      <p class="card-text">Write servers and services in .NET using the Dapr SDK. Includes support for ASP.NET.</p>
      <a href="https://github.com/dapr/dotnet-sdk/tree/master/examples/AspNetCore" class="stretched-link"></a>
    </div>
  </div>
  <div class="card">
    <div class="card-body">
      <h5 class="card-title"><b>Actors</b></h5>
      <p class="card-text">Create virtual actors with state, reminders/timers, and methods in .NET.</p>
      <a href="{{< ref dotnet-actors >}}" class="stretched-link"></a>
    </div>
  </div>
  <div class="card">
    <div class="card-body">
      <h5 class="card-title"><b>Workflow</b></h5>
      <p class="card-text">Create and manage workflows that work with other Dapr APIs in .NET.</p>
      <a href="{{< ref dotnet-workflow >}}" class="stretched-link"></a>
    </div>
  </div>
  <div class="card">
    <div class="card-body">
      <h5 class="card-title"><b>Jobs</b></h5>
      <p class="card-text">Create and manage the scheduling and orchestration of jobs in .NET.</p>
      <a href="{{< ref dotnet-jobs >}}" class="stretched-link"></a>
    </div>
  </div>
  <div class="card">
    <div class="card-body">
      <h5 class="card-title"><b>AI</b></h5>
      <p class="card-text">Create and manage AI operations in .NET</p>
      <a href="{{< ref dotnet-ai >}}" class="stretched-link"></a>
    </div>
  </div>
</div>

## More information

Learn more about local development options, or browse NuGet packages to add to your existing .NET applications.

<div class="card-deck">
  <div class="card">
    <div class="card-body">
      <h5 class="card-title"><b>Development</b></h5>
      <p class="card-text">Learn about local development options for .NET Dapr applications</p>
      <a href="{{< ref dotnet-development >}}" class="stretched-link"></a>
    </div>
  </div>
  <div class="card">
    <div class="card-body">
      <h5 class="card-title"><b>NuGet packages</b></h5>
      <p class="card-text">Dapr packages for adding the .NET SDKs to your .NET applications.</p>
      <a href="https://www.nuget.org/profiles/dapr.io" class="stretched-link"></a>
    </div>
  </div>
</div>
<br />