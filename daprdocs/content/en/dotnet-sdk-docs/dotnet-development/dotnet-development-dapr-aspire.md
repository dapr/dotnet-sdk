---
type: docs
title: "Dapr .NET SDK Development with .NET Aspire"
linkTitle: ".NET Aspire"
weight: 40000
description: Learn about local development with .NET Aspire
---

# .NET Aspire

[.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/get-started/aspire-overview) is a development tool 
designed to make it easier to include external software into .NET applications by providing a framework that allows 
third-party services to be readily integrated, observed and provisioned alongside your own software.

Aspire simplifies local development by providing rich integration with popular IDEs including 
[Microsoft Visual Studio](https://visualstudio.microsoft.com/vs/), 
[Visual Studio Code](https://code.visualstudio.com/), 
[JetBrains Rider](https://blog.jetbrains.com/dotnet/2024/02/19/jetbrains-rider-and-the-net-aspire-plugin/) and others 
to launch your application with the debugger while automatically launching and provisioning access to other 
integrations as well, including Dapr.

While Aspire also assists with deployment of your application to various cloud hosts like Microsoft Azure and 
Amazon AWS, deployment is currently outside the scope of this guide. More information can be found in Aspire's 
documentation [here](https://learn.microsoft.com/en-us/dotnet/aspire/deployment/overview).

## Prerequisites
- Both the Dapr .NET SDK and .NET Aspire are compatible with [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) 
or [.NET 9](https://dotnet.microsoft.com/download/dotnet/9.0)
- An OCI compliant container runtime such as [Docker Desktop](https://www.docker.com/products/docker-desktop) or 
[Podman](https://podman.io/)
- Install and initialize Dapr v1.16 or later

## Using .NET Aspire via CLI

We'll start by creating a brand new .NET application. Open your preferred CLI and navigate to the directory you wish
to create your new .NET solution within. Start by using the following command to install a template that will create
an empty Aspire application:

```sh
dotnet new install Aspire.ProjectTemplates
```

Once that's installed, proceed to create an empty .NET Aspire application in your current directory. The `-n` argument 
allows you to specify the name of the output solution. If it's excluded, the .NET CLI will instead use the name
of the output directory, e.g. `C:\source\aspiredemo` will result in the solution being named `aspiredemo`. The rest
of this tutorial will assume a solution named `aspiredemo`.

```sh
dotnet new aspire -n aspiredemo
```

This will create two Aspire-specific directories and one file in your directory:
- `aspiredemo.AppHost/` contains the Aspire orchestration project that is used to configure each of the integrations 
used in your application(s).
- `aspiredemo.ServiceDefaults/` contains a collection of extensions meant to be shared across your solution to aid in 
resilience, service discovery and telemetry capabilities offered by Aspire (these are distinct from the capabilities 
offered in Dapr itself).
- `aspiredemo.sln` is the file that maintains the layout of your current solution

We'll next create a project that'll serve as our Dapr application. From the same directory, use the following
to create an empty ASP.NET Core project called `MyApp`. This will be created relative to your current directory in 
`MyApp\MyApp.csproj`.

```sh
dotnet new web MyApp
```

Next we'll configure the AppHost project to add the necessary package to support local Dapr development. Navigate
into the AppHost directory with the following and install the `Aspire.Hosting.Dapr` package from NuGet into the project.
We'll also add a reference to our `MyApp` project so we can reference it during the registration process.

```sh
cd aspiredemo.AppHost
dotnet add package Aspire.Hosting.Dapr
dotnet add reference ../MyApp/
```

Next, we need to configure Dapr as a resource to be loaded alongside your project. Open the `Program.cs` file in that 
project within your preferred IDE. It should look similar to the following:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.Build().Run();
```

If you're familiar with the dependency injection approach used in ASP.NET Core projects or others utilizing the
`Microsoft.Extensions.DependencyInjection` functionality, you'll find that this will be a familiar experience.

Because we've already added a project reference to `MyApp`, we need to start by adding a reference in this configuration
as well. Add the following before the `builder.Build().Run()` line:

```csharp
var myApp = builder
    .AddProject<Projects.MyApp>("myapp")
    .WithDaprSidecar();
```

Because the project reference has been added to this solution, your project shows up as a type within the `Projects.`
namespace for our purposes here. The name of the variable you assign the project to doesn't much matter in this tutorial
but would be used if you wanted to create a reference between this project and another using Aspire's service discovery 
functionality.

Adding `.WithDaprSidecar()` configures Dapr as a .NET Aspire resource so that when the project runs, the sidecar will be
deployed alongside your application. This accepts a number of different options and could optionally be configured as in
the following example:

```csharp
DaprSidecarOptions sidecarOptions = new()
{
    AppId = "my-other-app",
    AppPort = 8080, //Note that this argument is required if you intend to configure pubsub, actors or workflows as of Aspire v9.0 
    DaprGrpcPort = 50001,
    DaprHttpPort = 3500,
    MetricsPort = 9090
};

builder
    .AddProject<Projects.MyOtherApp>("myotherapp")
    .WithReference(myApp)
    .WithDaprSidecar(sidecarOptions);
```

{{% alert color="primary" %}}

As indicated in the example above, as of .NET Aspire 9.0, if you intend to use any functionality in which Dapr needs to
call into your application such as pubsub, actors or workflows, you will need to specify your AppPort as
a configured option as Aspire will not automatically pass it to Dapr at runtime. It's expected that this behavior will
change in a future release as a fix has been merged and can be tracked [here](https://github.com/dotnet/aspire/pull/6362).

{{% /alert %}}

When you open the solution in your IDE, ensure that the `aspiredemo.AppHost` is configured as your startup project, but 
when you launch it in a debug configuration, you'll note that your integrated console should reflect your expected Dapr 
logs and it will be available to your application.

