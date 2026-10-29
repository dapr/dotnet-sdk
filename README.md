# Dapr SDK for .NET

[![NuGet Version](https://img.shields.io/nuget/v/Dapr.Client?logo=nuget&label=Latest%20version&style=flat)](https://www.nuget.org/packages/Dapr.Client) [![NuGet Downloads](https://img.shields.io/nuget/dt/Dapr.Client?style=flat&logo=nuget&label=Downloads)](https://www.nuget.org/packages/Dapr.Client) [![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/dapr/dotnet-sdk/.github%2Fworkflows%2Fsdk_build.yml?branch=master&label=Build&logo=github)](https://github.com/dapr/dotnet-sdk/actions/workflows/sdk_build.yml) [![codecov](https://codecov.io/gh/dapr/dotnet-sdk/branch/master/graph/badge.svg)](https://codecov.io/gh/dapr/dotnet-sdk) [![GitHub License](https://img.shields.io/github/license/dapr/dotnet-sdk?style=flat&label=License&logo=github)](https://github.com/dapr/dotnet-sdk/blob/master/LICENSE) [![GitHub issue custom search in repo](https://img.shields.io/github/issues-search/dapr/dotnet-sdk?query=type%3Aissue%20is%3Aopen%20label%3A%22good%20first%20issue%22&label=Good%20first%20issues&style=flat&logo=github)](https://github.com/dapr/dotnet-sdk/issues?q=is%3Aissue+is%3Aopen+label%3A%22good+first+issue%22) [![Discord](https://img.shields.io/discord/778680217417809931?label=Discord&style=flat&logo=discord)](http://bit.ly/dapr-discord) [![YouTube Channel Views](https://img.shields.io/youtube/channel/views/UCtpSQ9BLB_3EXdWAUQYwnRA?style=flat&label=YouTube%20views&logo=youtube)](https://youtube.com/@daprdev) [![X (formerly Twitter) Follow](https://img.shields.io/twitter/follow/daprdev?logo=x&style=flat)](https://twitter.com/daprdev)


Build durable workflows, actors, jobs, pub/sub messaging, state management and AI agents in .NET with [Dapr](https://dapr.io).

The Dapr SDK for .NET lets you:
- Write durable, long-running workflows in plain C# with Dapr Workflow
- Interact with Dapr applications through a Dapr client: service invocation, pub/sub, state, secrets, configuration, bindings and more
- Schedule jobs, use distributed locks, and encrypt and decrypt data
- Call large language models and build agents with Dapr AI
- Build routes and controllers in ASP.NET Core
- Implement the Virtual Actor model, based on the actor design pattern

This SDK can run locally, in a container, and in any distributed systems environment.

## Getting started

Install the client and the workflow packages from [nuget.org](https://www.nuget.org/profiles/dapr.io):

```bash
dotnet add package Dapr.Client
dotnet add package Dapr.Workflow
```

You also need the [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/) and an [initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/) to run your application next to a Dapr sidecar.

Then read:
- the [Dapr .NET SDK documentation](https://docs.dapr.io/developing-applications/sdks/dotnet/)
- the [Dapr Workflow overview](https://docs.dapr.io/developing-applications/building-blocks/workflow/workflow-overview/) and the [.NET workflow guide](https://docs.dapr.io/developing-applications/sdks/dotnet/dotnet-workflow/)

## Packages

We publish these [NuGet packages](https://www.nuget.org/profiles/dapr.io) to nuget.org for each release. Each package supports .NET 8, .NET 9 and .NET 10.

| Package | Use it to |
|---------|-----------|
| [Dapr.Client](https://www.nuget.org/packages/Dapr.Client) | Call the Dapr APIs: service invocation, pub/sub, state, secrets, configuration, bindings |
| [Dapr.AspNetCore](https://www.nuget.org/packages/Dapr.AspNetCore) | Integrate Dapr with ASP.NET Core routes, controllers and pub/sub subscriptions |
| [Dapr.Workflow](https://www.nuget.org/packages/Dapr.Workflow) | Author and run durable workflows |
| [Dapr.Actors](https://www.nuget.org/packages/Dapr.Actors) | Implement and call virtual actors |
| [Dapr.Actors.AspNetCore](https://www.nuget.org/packages/Dapr.Actors.AspNetCore) | Host actors in ASP.NET Core |
| [Dapr.Actors.Generators](https://www.nuget.org/packages/Dapr.Actors.Generators) | Generate strongly typed actor clients |
| [Dapr.Actors.Next](https://www.nuget.org/packages/Dapr.Actors.Next) | Develop actors with the newer actors SDK |
| [Dapr.AI](https://www.nuget.org/packages/Dapr.AI) | Call large language models through the Dapr Conversation API |
| [Dapr.AI.A2A](https://www.nuget.org/packages/Dapr.AI.A2A) | Connect agents with the Agent2Agent (A2A) protocol |
| [Dapr.AI.Microsoft.Extensions](https://www.nuget.org/packages/Dapr.AI.Microsoft.Extensions) | Use Dapr AI through Microsoft.Extensions.AI |
| [Dapr.Jobs](https://www.nuget.org/packages/Dapr.Jobs) | Schedule jobs |
| [Dapr.Messaging](https://www.nuget.org/packages/Dapr.Messaging) | Publish and subscribe through Dapr pub/sub components |
| [Dapr.StateManagement](https://www.nuget.org/packages/Dapr.StateManagement) | Use typed state management clients |
| [Dapr.SecretsManagement](https://www.nuget.org/packages/Dapr.SecretsManagement) | Use typed secret store clients |
| [Dapr.Extensions.Configuration](https://www.nuget.org/packages/Dapr.Extensions.Configuration) | Load secrets and configuration into `Microsoft.Extensions.Configuration` |
| [Dapr.BinaryStore](https://www.nuget.org/packages/Dapr.BinaryStore) | Store and stream binary data |
| [Dapr.Cryptography](https://www.nuget.org/packages/Dapr.Cryptography) | Encrypt and decrypt data |
| [Dapr.DistributedLock](https://www.nuget.org/packages/Dapr.DistributedLock) | Acquire distributed locks |
| [Dapr.Metadata](https://www.nuget.org/packages/Dapr.Metadata) | Read sidecar metadata |
| [Dapr.Search](https://www.nuget.org/packages/Dapr.Search) | Index and query documents (alpha API) |
| [Dapr.Vector](https://www.nuget.org/packages/Dapr.Vector) | Store and query vector embeddings (alpha API) |
| [Dapr.Testcontainers](https://www.nuget.org/packages/Dapr.Testcontainers) | Run Dapr in integration tests with Testcontainers |
| [Dapr.Testcontainers.Xunit](https://www.nuget.org/packages/Dapr.Testcontainers.Xunit) | Use Dapr.Testcontainers from xUnit |

The repo also builds [Dapr.Common](https://www.nuget.org/packages/Dapr.Common) and [Dapr.Protos](https://www.nuget.org/packages/Dapr.Protos). They hold types shared by the packages above and are not intended for direct use.

## Documentation

The docs for the Dapr .NET SDK can be found on the [Dapr docs site](https://docs.dapr.io/developing-applications/sdks/dotnet/).

## Examples

The [examples folder](https://github.com/dapr/dotnet-sdk/tree/master/examples) has examples to get you up and running with the Dapr .NET SDK, including:
- [Workflow examples](https://github.com/dapr/dotnet-sdk/tree/master/examples/Workflow): task chaining, fan-out/fan-in, external events, child workflows, retries, versioning and more
- [AI examples](https://github.com/dapr/dotnet-sdk/tree/master/examples/AI): the Conversation API and Microsoft.Extensions.AI integration

## Contributing

Also make sure to read the [.NET SDK contribution guide](https://docs.dapr.io/contributing/sdk-contrib/dotnet-contributing/) and [CONTRIBUTING.md](https://github.com/dapr/dotnet-sdk/blob/master/CONTRIBUTING.md).

### Prerequisites

Each project is a normal C# project. The repo's `global.json` pins the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (10.0.100 or a later feature band), which you need to build, test, and generate NuGet packages. The libraries target .NET 8, .NET 9 and .NET 10, so install the [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) and [.NET 9](https://dotnet.microsoft.com/download/dotnet/9.0) runtimes too if you want to run the tests on every framework.

**macOS/Linux:**

On macOS or Linux we recommend [Visual Studio Code](https://code.visualstudio.com/) with the [C# Dev Kit extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit). See [here](https://code.visualstudio.com/docs/languages/dotnet) for a getting started guide for VS Code and .NET. [JetBrains Rider](https://www.jetbrains.com/rider/) also works.

**Windows:**

On Windows, we recommend the [latest Visual Studio](https://visualstudio.microsoft.com/vs/). Visual Studio 2022 does not support the .NET 10 SDK, so use Visual Studio 2026 or later. It sets you up with all the .NET build tools and opens the solution files. Community Edition is free and can be used to build everything here.

Make sure you [update Visual Studio to the most recent release](https://learn.microsoft.com/visualstudio/install/update-visual-studio).


### Build

To build everything and generate NuGet packages, run dotnet cli commands. Binaries and NuGet packages will be dropped in a *bin* directory at the repo root.

```bash
# Build sdk, samples and tests.
dotnet build -c Debug  # for release, -c Release

# Run unit-test
dotnet test

# Generate nuget packages in /bin/Debug/nugets
dotnet pack
```

Each project can also be built individually directly through the CLI or your editor/IDE. You can open the solution file all.sln in repo root to load all sdk, samples and test projects.

### Using NuGet packages built locally in your project

\<RepoRoot\> is the path where you cloned this repository.
NuGet packages are dropped under *<RepoRoot>/bin/<Debug|Release>/nugets* when you build locally.

```bash
# Add Dapr.Actors nuget package
dotnet add package Dapr.Actors -s <RepoRoot>/bin/<Debug|Release>/nugets

# Add Dapr.Actors.AspNetCore nuget package
dotnet add package Dapr.Actors.AspNetCore -s <RepoRoot>/bin/<Debug|Release>/nugets
```
