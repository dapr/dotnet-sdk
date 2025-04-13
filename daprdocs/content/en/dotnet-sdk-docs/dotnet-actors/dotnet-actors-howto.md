---
type: docs
title: "How to: Run and use virtual actors in the .NET SDK"
linkTitle: "How to: Run & use virtual actors"
weight: 300000
description: Try out .NET Dapr virtual actors with this example
---

The Dapr actor package allows you to interact with Dapr virtual actors from a .NET application. In this guide, you learn how to:

- Create an Actor (`MyActor`).
- Invoke its methods on the client application.

```
MyActor --- MyActor.Interfaces
         |
         +- MyActorService
         |
         +- MyActorClient
```

**The interface project (\MyActor\MyActor.Interfaces)** 

This project contains the interface definition for the actor. Actor interfaces can be defined in any project with any name. The interface defines the actor contract shared by:

- The actor implementation
- The clients calling the actor

Because client projects may depend on it, it's better to define it in an assembly separate from the actor implementation.

**The actor service project (\MyActor\MyActorService)** 

This project implements the ASP.Net Core web service that hosts the actor. It contains the implementation of the actor, `MyActor.cs`. An actor implementation is a class that:

- Derives from the base type Actor
- Implements the interfaces defined in the `MyActor.Interfaces` project. 

An actor class must also implement a constructor that accepts an `ActorService` instance and an `ActorId`, and passes them to the base Actor class.

**The actor client project (\MyActor\MyActorClient)** 

This project contains the implementation of the actor client which calls MyActor's method defined in Actor Interfaces.

## Prerequisites

- [Dapr CLI]({{< ref install-dapr-cli.md >}}) installed.
- Initialized [Dapr environment]({{< ref install-dapr-selfhost.md >}}).
- [.NET 8](https://dotnet.microsoft.com/download) or [.NET 9](https://dotnet.microsoft.com/download) installed

## Step 0: Prepare

Since we'll be creating 3 projects, choose an empty directory to start from, and open it in your terminal of choice.

## Step 1: Create actor interfaces

Actor interface defines the actor contract that is shared by the actor implementation and the clients calling the actor.

Actor interface is defined with the below requirements:

- Actor interface must inherit `Dapr.Actors.IActor` interface
- The return type of Actor method must be `Task` or `Task<object>`
- Actor method can have one argument at a maximum

### Create interface project and add dependencies

```bash
# Create Actor Interfaces
dotnet new classlib -o MyActor.Interfaces

cd MyActor.Interfaces

# Add Dapr.Actors nuget package. Please use the latest package version from nuget.org
dotnet add package Dapr.Actors

cd ..
```

### Implement IMyActor interface

Define `IMyActor` interface and `MyData` data object. Paste the following code into `MyActor.cs` in the `MyActor.Interfaces` project.

```csharp
using Dapr.Actors;
using Dapr.Actors.Runtime;
using System.Threading.Tasks;

namespace MyActor.Interfaces
{
    public interface IMyActor : IActor
    {       
        Task<string> SetDataAsync(MyData data);
        Task<MyData> GetDataAsync();
        Task RegisterReminder();
        Task UnregisterReminder();
        Task<IActorReminder> GetReminder();
        Task RegisterTimer();
        Task UnregisterTimer();
    }

    public class MyData
    {
        public string PropertyA { get; set; }
        public string PropertyB { get; set; }

        public override string ToString()
        {
            var propAValue = this.PropertyA == null ? "null" : this.PropertyA;
            var propBValue = this.PropertyB == null ? "null" : this.PropertyB;
            return $"PropertyA: {propAValue}, PropertyB: {propBValue}";
        }
    }
}
```

## Step 2: Create actor service

Dapr uses ASP.NET web service to host Actor service. This section will implement `IMyActor` actor interface and register Actor to Dapr Runtime.

### Create actor service project and add dependencies

```bash
# Create ASP.Net Web service to host Dapr actor
dotnet new web -o MyActorService

cd MyActorService

# Add Dapr.Actors.AspNetCore nuget package. Please use the latest package version from nuget.org
dotnet add package Dapr.Actors.AspNetCore

# Add Actor Interface reference
dotnet add reference ../MyActor.Interfaces/MyActor.Interfaces.csproj

cd ..
```

### Add actor implementation

Implement IMyActor interface and derive from `Dapr.Actors.Actor` class. Following example shows how to use Actor Reminders as well. For Actors to use Reminders, it must derive from IRemindable. If you don't intend to use Reminder feature, you can skip implementing IRemindable and reminder specific methods which are shown in the code below.

Paste the following code into `MyActor.cs` in the `MyActorService` project:

```csharp
using Dapr.Actors;
using Dapr.Actors.Runtime;
using MyActor.Interfaces;
using System;
using System.Threading.Tasks;

namespace MyActorService
{
    internal class MyActor : Actor, IMyActor, IRemindable
    {
        // The constructor must accept ActorHost as a parameter, and can also accept additional
        // parameters that will be retrieved from the dependency injection container
        //
        /// <summary>
        /// Initializes a new instance of MyActor
        /// </summary>
        /// <param name="host">The Dapr.Actors.Runtime.ActorHost that will host this actor instance.</param>
        public MyActor(ActorHost host)
            : base(host)
        {
        }

        /// <summary>
        /// This method is called whenever an actor is activated.
        /// An actor is activated the first time any of its methods are invoked.
        /// </summary>
        protected override Task OnActivateAsync()
        {
            // Provides opportunity to perform some optional setup.
            Console.WriteLine($"Activating actor id: {this.Id}");
            return Task.CompletedTask;
        }

        /// <summary>
        /// This method is called whenever an actor is deactivated after a period of inactivity.
        /// </summary>
        protected override Task OnDeactivateAsync()
        {
            // Provides Opporunity to perform optional cleanup.
            Console.WriteLine($"Deactivating actor id: {this.Id}");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Set MyData into actor's private state store
        /// </summary>
        /// <param name="data">the user-defined MyData which will be stored into state store as "my_data" state</param>
        public async Task<string> SetDataAsync(MyData data)
        {
            // Data is saved to configured state store implicitly after each method execution by Actor's runtime.
            // Data can also be saved explicitly by calling this.StateManager.SaveStateAsync();
            // State to be saved must be DataContract serializable.
            await this.StateManager.SetStateAsync<MyData>(
                "my_data",  // state name
                data);      // data saved for the named state "my_data"

            return "Success";
        }

        /// <summary>
        /// Get MyData from actor's private state store
        /// </summary>
        /// <return>the user-defined MyData which is stored into state store as "my_data" state</return>
        public Task<MyData> GetDataAsync()
        {
            // Gets state from the state store.
            return this.StateManager.GetStateAsync<MyData>("my_data");
        }

        /// <summary>
        /// Register MyReminder reminder with the actor
        /// </summary>
        public async Task RegisterReminder()
        {
            await this.RegisterReminderAsync(
                "MyReminder",              // The name of the reminder
                null,                      // User state passed to IRemindable.ReceiveReminderAsync()
                TimeSpan.FromSeconds(5),   // Time to delay before invoking the reminder for the first time
                TimeSpan.FromSeconds(5));  // Time interval between reminder invocations after the first invocation
        }

        /// <summary>
        /// Get MyReminder reminder details with the actor
        /// </summary>
        public async Task<IActorReminder> GetReminder()
        {
            await this.GetReminderAsync("MyReminder");
        }

        /// <summary>
        /// Unregister MyReminder reminder with the actor
        /// </summary>
        public Task UnregisterReminder()
        {
            Console.WriteLine("Unregistering MyReminder...");
            return this.UnregisterReminderAsync("MyReminder");
        }

        // <summary>
        // Implement IRemindeable.ReceiveReminderAsync() which is call back invoked when an actor reminder is triggered.
        // </summary>
        public Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        {
            Console.WriteLine("ReceiveReminderAsync is called!");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Register MyTimer timer with the actor
        /// </summary>
        public Task RegisterTimer()
        {
            return this.RegisterTimerAsync(
                "MyTimer",                  // The name of the timer
                nameof(this.OnTimerCallBack),       // Timer callback
                null,                       // User state passed to OnTimerCallback()
                TimeSpan.FromSeconds(5),    // Time to delay before the async callback is first invoked
                TimeSpan.FromSeconds(5));   // Time interval between invocations of the async callback
        }

        /// <summary>
        /// Unregister MyTimer timer with the actor
        /// </summary>
        public Task UnregisterTimer()
        {
            Console.WriteLine("Unregistering MyTimer...");
            return this.UnregisterTimerAsync("MyTimer");
        }

        /// <summary>
        /// Timer callback once timer is expired
        /// </summary>
        private Task OnTimerCallBack(byte[] data)
        {
            Console.WriteLine("OnTimerCallBack is called!");
            return Task.CompletedTask;
        }
    }
}
```

### Register actor runtime with ASP.NET Core startup

The Actor runtime is configured through ASP.NET Core `Startup.cs`. 

The runtime uses the ASP.NET Core dependency injection system to register actor types and essential services. This integration is provided through the `AddActors(...)` method call in `ConfigureServices(...)`. Use the delegate passed to `AddActors(...)` to register actor types and configure actor runtime settings. You can register additional types for dependency injection inside `ConfigureServices(...)`. These will be available to be injected into the constructors of your Actor types.

Actors are implemented via HTTP calls with the Dapr runtime. This functionality is part of the application's HTTP processing pipeline and is registered inside `UseEndpoints(...)` inside `Configure(...)`.

Paste the following code into `Startup.cs` in the `MyActorService` project:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MyActorService
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddActors(options =>
            {
                // Register actor types and configure actor settings
                options.Actors.RegisterActor<MyActor>();
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            // Register actors handlers that interface with the Dapr runtime.
            app.MapActorsHandlers();
        }
    }
}
```

## Step 3: Add a client

Create a simple console app to call the actor service. Dapr SDK provides Actor Proxy client to invoke actor methods defined in Actor Interface.

### Create actor client project and add dependencies

```bash
# Create Actor's Client
dotnet new console -o MyActorClient

cd MyActorClient

# Add Dapr.Actors nuget package. Please use the latest package version from nuget.org
dotnet add package Dapr.Actors

# Add Actor Interface reference
dotnet add reference ../MyActor.Interfaces/MyActor.Interfaces.csproj

cd ..
```

### Invoke actor methods with strongly-typed client

You can use `ActorProxy.Create<IMyActor>(..)` to create a strongly-typed client and invoke methods on the actor.

Paste the following code into `Program.cs` in the `MyActorClient` project:

```csharp
using System;
using System.Threading.Tasks;
using Dapr.Actors;
using Dapr.Actors.Client;
using MyActor.Interfaces;

namespace MyActorClient
{
    class Program
    {
        static async Task MainAsync(string[] args)
        {
            Console.WriteLine("Startup up...");

            // Registered Actor Type in Actor Service
            var actorType = "MyActor";

            // An ActorId uniquely identifies an actor instance
            // If the actor matching this id does not exist, it will be created
            var actorId = new ActorId("1");

            // Create the local proxy by using the same interface that the service implements.
            //
            // You need to provide the type and id so the actor can be located. 
            var proxy = ActorProxy.Create<IMyActor>(actorId, actorType);

            // Now you can use the actor interface to call the actor's methods.
            Console.WriteLine($"Calling SetDataAsync on {actorType}:{actorId}...");
            var response = await proxy.SetDataAsync(new MyData()
            {
                PropertyA = "ValueA",
                PropertyB = "ValueB",
            });
            Console.WriteLine($"Got response: {response}");

            Console.WriteLine($"Calling GetDataAsync on {actorType}:{actorId}...");
            var savedData = await proxy.GetDataAsync();
            Console.WriteLine($"Got response: {savedData}");
        }
    }
}
```

## Running the code

The projects that you've created can now to test the sample.

1. Run MyActorService

    Since `MyActorService` is hosting actors, it needs to be run with the Dapr CLI.

    ```bash
    cd MyActorService
    dapr run --app-id myapp --app-port 5000 --dapr-http-port 3500 -- dotnet run
    ```

    You will see commandline output from both `daprd` and `MyActorService` in this terminal. You should see something like the following, which indicates that the application started successfully.

    ```txt
    ...
    ℹ️  Updating metadata for app command: dotnet run
    ✅  You're up and running! Both Dapr and your app logs will appear here.

    == APP == info: Microsoft.Hosting.Lifetime[0]

    == APP ==       Now listening on: https://localhost:5001

    == APP == info: Microsoft.Hosting.Lifetime[0]

    == APP ==       Now listening on: http://localhost:5000

    == APP == info: Microsoft.Hosting.Lifetime[0]

    == APP ==       Application started. Press Ctrl+C to shut down.

    == APP == info: Microsoft.Hosting.Lifetime[0]

    == APP ==       Hosting environment: Development

    == APP == info: Microsoft.Hosting.Lifetime[0]

    == APP ==       Content root path: /Users/ryan/actortest/MyActorService
    ```

2. Run MyActorClient

    `MyActorClient` is acting as the client, and it can be run normally with `dotnet run`.

    Open a new terminal an navigate to the `MyActorClient` directory. Then run the project with:

    ```bash
    dotnet run
    ```

    You should see commandline output like:

    ```txt
    Startup up...
    Calling SetDataAsync on MyActor:1...
    Got response: Success
    Calling GetDataAsync on MyActor:1...
    Got response: PropertyA: ValueA, PropertyB: ValueB
    ```

> 💡 This sample relies on a few assumptions. The default listening port for an ASP.NET Core web project is 5000, which is being passed to `dapr run` as `--app-port 5000`. The default HTTP port for the Dapr sidecar is 3500. We're telling the sidecar for `MyActorService` to use 3500 so that `MyActorClient` can rely on the default value.

Now you have successfully created an actor service and client. See the related links section to learn more.

## Related links

- [.NET Dapr Actors client guide]({{< ref dotnet-actors-client.md >}})
- [.NET Dapr Actors usage guide]({{< ref dotnet-actors-usage.md >}})
