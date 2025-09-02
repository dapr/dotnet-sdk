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
| [Quickstarts]({{% ref quickstarts %}}) | Experience Dapr's API building blocks in just a few minutes using the .NET SDK. |
| [SDK samples](https://github.com/dapr/dotnet-sdk/tree/master/examples) | Clone the SDK repo to try out some examples and get started. |
| [Pub/sub tutorial](https://github.com/dapr/quickstarts/tree/master/tutorials/pub-sub) | See how Dapr .NET SDK works alongside other Dapr SDKs to enable pub/sub applications. |

## Available packages

| Package Name                                                                                              | Documentation Link                                            | Description                                                                                                                                         |
|-----------------------------------------------------------------------------------------------------------|---------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------|
| [Dapr.Client](https://www.nuget.org/packages/Dapr.Client)                                                 | [Documentation]({{% ref dotnet-client %}})                    | Create .NET clients that interact with a Dapr sidecar and other Dapr applications.                                                                  |
| [Dapr.AI](https://www.nuget.org/packages/Dapr.AI)                                                         | [Documentation]({{% ref dotnet-ai %}})                        | Create and manage AI operations in .NET.                                                                                                            |
| [Dapr.AI.A2a](https://www.nuget.org/packages/Dapr.AI.A2a)                                                 |                                                               | Dapr SDK for implementing agent-to-agent operations using the [A2A](https://github.com/a2aproject/a2a-dotnet) framework.                            |
| [Dapr.AI.Microsoft.Extensions](https://www.nuget.org/packages/Dapr.AI.Microsoft.Extensions)               | [Documentation]({{% ref dotnet-ai-extensions-howto %}})       | Easily interact with LLMs conversationally and using tooling via the Dapr Conversation building block.                                              |   
| [Dapr.AspNetCore](https://www.nuget.org/packages/Dapr.AspNetCore)                                         | [Documentation]({{% ref dotnet-server %}})                    | Write servers and services in .NET using the Dapr SDK. Includes support and utilities providing richer integration with ASP.NET Core.               |
| [Dapr.Actors](https://www.nuget.org/packages/Dapr.Actors)                                                 | [Documentation]({{% ref dotnet-actors %}})                    | Create virtual actors with state, reminders/timers, and methods.                                                                                    |
| [Dapr.Actors.AspNetCore](https://www.nuget.org/packages/Dapr.Actors)                                      | [Documentation]({{% ref dotnet-actors %}})                    | Create virtual actors with state, reminders/timers, and methods with rich integration with ASP.NET Core.                                            |
| [Dapr.Actors.Analyzers](https://www.nuget.org/packages/Dapr.Actors.Analyzers)                             | [Documentation]({{% ref dotnet-guidance-source-generators %}}) | A collection of Roslyn source generators and analyzers for enabling better practices and preventing common errors when using Dapr Actors in .NET    |
| [Dapr.Cryptography](https://www.nuget.org/packages/Dapr.Cryptography)                                     | [Documentation]({{% dotnet-cryptography %}})                  | Encrypt and decrypt streaming state of any size using Dapr's cryptography building block.                                                           |
| [Dapr.Jobs](https://www.nuget.org/packages/Dapr.Jobs)                                                     | [Documentation]({{% ref dotnet-jobs %}})                      | Create and manage the scheduling and orchestration of jobs.                                                                                         |
| [Dapr.DistributedLocks](https://www.nuget.org/packages/Dapr.DistributedLocks)                             | [Documentation]({{% ref dotnet-distributed-lock %}})          | Create and manage distributed locks for managing exclusive resource access.                                                                         |
| [Dapr.Extensions.Configuration](https://www.nuget.org/packages/Dapr.Extensions.Configuration)             |                                                               | Dapr secret store configuration provider implementation for `Microsoft.Extensions.Configuration`.                                                   |
| [Dapr.PluggableComponents](https://www.nuget.org/packages/Dapr.PluggableComponents)         |                                                               | Used to implement pluggable components with Dapr using .NET.                                                                                        |
| [Dapr.PluggableComponents.AspNetCore](https://www.nuget.org/packages/Dapr.PluggableComponents.AspNetCore) |                                                               | Implement pluggable components with Dapr using .NET with rich ASP.NET Core support.                                                                 |
| [Dapr.PluggableComponents.Protos](https://www.nuget.org/packages/Dapr.PluggableComponents.Protos)         |                                                               | **Note:** Developers needn't install this package directly in their applications.                                                                   |
| [Dapr.Messaging](https://www.nuget.org/packages/Dapr.Messaging)                                           | [Documentation]({{% ref dotnet-messaging %}})                 | Build distributed applications using the Dapr Messaging SDK that utilize messaging components like streaming pub/sub subscriptions.                 |
| [Dapr.Workflow](https://www.nuget.org/packages/Dapr.Workflow)                                             | [Documentation]({{% ref dotnet-workflow %}})                  | Create and manage workflows that work with other Dapr APIs.                                                                                         |
| [Dapr.Workflow.Analyzers](https://www.nuget.org/packages/Dapr.Workflow.Analyzers)                         | [Documentation]({{% ref dotnet-guidance-source-generators %}}) | A collection of Roslyn source generators and analyzers for enabling better practices and preventing common errors when using Dapr Workflows in .NET |

## More information

Learn more about local development options, best practices, or browse NuGet packages to add to your existing .NET 
applications.

<div class="card-deck">
  <div class="card">
    <div class="card-body">
      <h5 class="card-title"><b>Development</b></h5>
      <p class="card-text">Learn about local development integration options</p>
      <a href="{{% ref dotnet-integrations %}}" class="stretched-link"></a>
    </div>
  <div class="card">
    <div class="card-body">
      <h5 class="card-title"><b>Best Practices</b></h5>
      <p class="card-text">Learn about best practices for developing .NET Dapr applications</p>
      <a href="{{% ref dotnet-guidance %}}" class="stretched-link"></a>
    </div>
  </div>
  <div class="card">
    <div class="card-body">
      <h5 class="card-title"><b>NuGet packages</b></h5>
      <p class="card-text">NuGet packages for adding the Dapr to your .NET applications.</p>
      <a href="https://www.nuget.org/profiles/dapr.io" class="stretched-link"></a>
    </div>
  </div>
</div>
<br />