# Dapr SDK for .NET

[![NuGet Version](https://img.shields.io/nuget/v/Dapr.Client?logo=nuget&label=Latest%20version&style=flat)](https://www.nuget.org/packages/Dapr.Client) [![NuGet Downloads](https://img.shields.io/nuget/dt/Dapr.Client?style=flat&logo=nuget&label=Downloads)](https://www.nuget.org/packages/Dapr.Client) [![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/dapr/dotnet-sdk/.github%2Fworkflows%2Fsdk_build.yml?branch=master&label=Build&logo=github)](https://github.com/dapr/dotnet-sdk/actions/workflows/sdk_build.yml) [![codecov](https://codecov.io/gh/dapr/dotnet-sdk/branch/master/graph/badge.svg)](https://codecov.io/gh/dapr/dotnet-sdk) [![GitHub License](https://img.shields.io/github/license/dapr/dotnet-sdk?style=flat&label=License&logo=github)](https://github.com/dapr/dotnet-sdk/blob/master/LICENSE) [![GitHub issue custom search in repo](https://img.shields.io/github/issues-search/dapr/dotnet-sdk?query=type%3Aissue%20is%3Aopen%20label%3A%22good%20first%20issue%22&label=Good%20first%20issues&style=flat&logo=github)](https://github.com/dapr/dotnet-sdk/issues?q=is%3Aissue+is%3Aopen+label%3A%22good+first+issue%22) [![Discord](https://img.shields.io/discord/778680217417809931?label=Discord&style=flat&logo=discord)](http://bit.ly/dapr-discord) [![YouTube Channel Views](https://img.shields.io/youtube/channel/views/UCtpSQ9BLB_3EXdWAUQYwnRA?style=flat&label=YouTube%20views&logo=youtube)](https://youtube.com/@daprdev) [![X (formerly Twitter) Follow](https://img.shields.io/twitter/follow/daprdev?logo=x&style=flat)](https://twitter.com/daprdev)


Dapr SDK for .NET allows you to:
- Interact with Dapr applications through a Dapr client
- Build routes and controllers in ASP.NET
- Implement the Virtual Actor model, based on the actor design pattern

This SDK can run locally, in a container, and in any distributed systems environment.

## Releases

We publish [nuget packages](https://www.nuget.org/profiles/dapr.io) to nuget.org for each release.

### Using nugets built locally in your project

\<RepoRoot\> is the path where you cloned this repository.
Nuget packages are dropped under *<RepoRoot>/bin/<Debug|Release>/nugets* when you build locally.

**Example**
```bash
# Add Dapr.Actors nuget package
dotnet add package Dapr.Actors -s <RepoRoot>/bin/<Debug|Release>/nugets

# Add Dapr.Actors.AspNetCore nuget package
dotnet add package Dapr.Actors.AspNetCore -s <RepoRoot>/bin/<Debug|Release>/nugets
```

## Documentation

The docs for the Dapr .NET SDK can be found on the [Dapr docs site](https://docs.dapr.io/developing-applications/sdks/dotnet/).

## Examples

Visit the [examples folder](./examples) for a variety of examples to get you up and running with the Dapr .NET SDK.

## Contributing

This repo builds the following packages:

- Dapr.Client
- Dapr.AspNetCore
- Dapr.Actors
- Dapr.Actors.AspNetCore
- Dapr.Actors.Generators
- Dapr.AI
- Dapr.Jobs
- Dapr.Messaging
- Dapr.Extensions.Configuration
- Dapr.Workflow

It also builds the following packages which are not intended for public use and contain common types used in the packages above:
- Dapr.Common
- Dapr.Protos


### Prerequisites

Each project is a normal C# project. At minimum, you need [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build, test, and generate NuGet packages.

Also make sure to reference the [.NET SDK contribution guide](https://docs.dapr.io/contributing/sdk-contrib/dotnet-contributing/)

**macOS/Linux:**

On macOS or Linux we recommend [Visual Studio Code](https://code.visualstudio.com/) with the [C# Extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp). See [here](https://code.visualstudio.com/docs/languages/dotnet) for a getting started guide for VS Code and .NET.

**Windows:**

On Windows, we recommend installing [the latest Visual Studio 2022](https://www.visualstudio.com/vs/) which will set you up with all the .NET build tools and allow you to open the solution files. Community Edition is free and can be used to build everything here.

Make sure you [update Visual Studio to the most recent release](https://docs.microsoft.com/visualstudio/install/update-visual-studio).


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
