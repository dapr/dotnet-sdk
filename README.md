# Dapr SDK for .NET

Dapr is a portable, event-driven, serverless runtime for building distributed applications across cloud and edge.

[![Build Status](https://github.com/dapr/dotnet-sdk/workflows/build/badge.svg)](https://github.com/dapr/dotnet-sdk/actions?workflow=build)
[![Gitter](https://badges.gitter.im/Dapr/community.svg)](https://gitter.im/Dapr/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge)
[![codecov](https://codecov.io/gh/dapr/dotnet-sdk/branch/master/graph/badge.svg)](https://codecov.io/gh/dapr/dotnet-sdk)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

- [dapr.io](https://dapr.io)
- [@DaprDev](https://twitter.com/DaprDev)


Dapr SDK for .NET allows you to implement the Virtual Actor model, based on the actor design pattern. This SDK can run locally, in a container and in any distributed systems environment.

This repo builds the following packages:

- Dapr.Client
- Dapr.AspNetCore
- Dapr.Actors
- Dapr.Actors.AspNetCore
- Dapr.Extensions.Configuration

## Getting Started

### Prerequesites

Each project is a normal C# project. At minimum, you need [.NET Core SDK 3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1) to build, test, and generate NuGet packages.

**macOS/Linux:**

On macOS or Linux we recommend [Visual Studio Code](https://code.visualstudio.com/) with the [C# Extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp). See [here](https://code.visualstudio.com/docs/languages/dotnet) for a getting started guide for VS Code and .NET.


**Windows:**

On Windows, we recommend installing [the latest Visual Studio 2019](https://www.visualstudio.com/vs/) which will set you up with all the .NET build tools and allow you to open the solution files. Community Edition is free and can be used to build everything here.

Make sure you [update Visual Studio to the most recent release](https://docs.microsoft.com/visualstudio/install/update-visual-studio).



### Solution Files
The repo currently has 4 solution files:
- *all.sln*: This includes all the sdk product, test and samples project files.
- *src/prod.sln*: This includes all the product project files.
- *samples/samples.sln*: This includes all the sample projects files dependencies project files.
- *test/test.sln*: This includes all the test projects and dependencies project files.

### Build

To build everything and generate NuGet packages, run dotnet cli commands. Binaries and NuGet packages will be dropped in a *bin* directory at the repo root.

```bash
# Build sdk, samples and tests.
dotnet build -c Debug  # for release, -c Release

# Run unit-test
dotnet test test/test.sln

# Generate nuget packages in /bin/Debug/nugets
dotnet pack src/prod.sln
```

Each project can also be built individually directly through the CLI or your editor/IDE. You can open the solution file all.sln in repo root to load all sdk, samples and test projects.

## Releases

We publish nuget packages to nuget.org for each release.

## Using nugets built locally in your project

\<RepoRoot\> is the path where you cloned this repository.
Nuget packages are dropped under *<RepoRoot>/bin/<Debug|Release>/nugets* when you build locally.

Examples:
```bash
# Add Dapr.Actors nuget package
dotnet add package Dapr.Actors -s <RepoRoot>/bin/<Debug|Release>/nugets

# Add Dapr.Actors.AspNetCore nuget package
dotnet add package Dapr.Actors.AspNetCore -s <RepoRoot>/bin/<Debug|Release>/nugets
```

## Documentation

These articles will help get you started with Dapr runtime and Dapr Actors:

- [Getting started with Dapr Actor](docs/get-started-dapr-actor.md)
- [Dapr CLI](https://github.com/dapr/cli)
- [Dapr Actors API Reference](https://github.com/dapr/docs/blob/master/reference/api/actors.md)
