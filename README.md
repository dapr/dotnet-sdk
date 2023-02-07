# Dapr SDK for .NET

[![Build Status](https://github.com/dapr/dotnet-sdk/workflows/build/badge.svg)](https://github.com/dapr/dotnet-sdk/actions?workflow=build)
[![codecov](https://codecov.io/gh/dapr/dotnet-sdk/branch/master/graph/badge.svg)](https://codecov.io/gh/dapr/dotnet-sdk)
[![License: Apache](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](http://www.apache.org/licenses/LICENSE-2.0)
[![FOSSA Status](https://app.fossa.com/api/projects/custom%2B162%2Fgithub.com%2Fdapr%2Fcomponents-contrib.svg?type=shield)](https://app.fossa.com/projects/custom%2B162%2Fgithub.com%2Fdapr%2Fcomponents-contrib?ref=badge_shield)


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
- Dapr.Extensions.Configuration
- Dapr.Workflow

### Prerequisites

Each project is a normal C# project. At minimum, you need [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) to build, test, and generate NuGet packages.

Also make sure to reference the [.NET SDK contribution guide](https://docs.dapr.io/contributing/sdk-contrib/dotnet-contributing/)

**macOS/Linux:**

On macOS or Linux we recommend [Visual Studio Code](https://code.visualstudio.com/) with the [C# Extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp). See [here](https://code.visualstudio.com/docs/languages/dotnet) for a getting started guide for VS Code and .NET.

**Windows:**

On Windows, we recommend installing [the latest Visual Studio 2019](https://www.visualstudio.com/vs/) which will set you up with all the .NET build tools and allow you to open the solution files. Community Edition is free and can be used to build everything here.

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
