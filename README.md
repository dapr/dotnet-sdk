# Dapr SDK for .NET

Dapr is a programming model for writing cloud-native applications which are distributed, dynamically scaled, and loosely coupled in nature.

Dapr SDK for .NET allows you to implement the Virtual Actor model, based on the actor design pattern. This SDK can run locally, in a container and in any distributed systems environment.

This repo builds the following packages:

- Dapr.Client
- Dapr.Client.Grpc
- Dapr.AspNetCore
- Dapr.Actors
- Dapr.Actors.AspNetCore

## Getting Started

### Prerequesites

Each project is a normal C# Visual Studio 2019 project. At minimum, you need [.NET Core SDK 3.0](https://dotnet.microsoft.com/download/dotnet-core/3.0) to build and generate NuGet packages.

We recommend installing [Visual Studio 2019 v16.3 or later ](https://www.visualstudio.com/vs/) which will set you up with all the .NET build tools and allow you to open the solution files. Community Edition is free and can be used to build everything here.
Make sure you [update Visual Studio to the most recent release](https://docs.microsoft.com/visualstudio/install/update-visual-studio). To find a version of .NET Core that can be used with earlier versions of Visual Studio, see [.NET SDKs for Visual Studio](https://dotnet.microsoft.com/download/visual-studio-sdks).

### Build

To build everything and generate NuGet packages, run dotnet cli commands. NuGet packages will be dropped in a *bin* directory at the repo root.

```bash
# Build SDK
dotnet build -c Debug  # for release, -c Release

# Run unit-test
dotnet test

# Generate nuget packages in /bin/Debug/
dotnet pack
```

Each project can also be built individually directly through Visual Studio.

## Releases

We publish nuget packages to nuget.org for each release.

## Using nugets built locally in your project

RepoRoot is the path where you cloned this repository.

```bash
# Add Dapr.Actors nuget package
dotnet add package Dapr.Actors -s <RepoRoot>/bin/<Debug|Release>/

# Add Dapr.Actors.AspNetCore nuget package
dotnet add package Dapr.Actors.AspNetCore -s <RepoRoot>/bin/<Debug|Release>/
```

## Documentation

These articles will help get you started with Dapr runtime and Dapr Actors:

- [Getting started with Dapr Actor](docs/get-started-dapr-actor.md)
- [Dapr CLI](https://github.com/dapr/cli)
- [Dapr Actors API Reference](https://github.com/dapr/docs/blob/master/reference/api/actors.md)
