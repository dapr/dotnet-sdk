---
type: docs
title: "Getting started with the Dapr actor .NET SDK"
linkTitle: "Example"
weight: 100000
description: Try out .NET virtual actors with this example
---

## Prerequisites

- [Dapr CLI]({{< ref install-dapr-cli.md >}}) installed
- Initialized [Dapr environment]({{< ref install-dapr-selfhost.md >}})
- [.NET 3.1+](https://dotnet.microsoft.com/download) installed

## Overview

This document describes how to create an Actor(`MyActor`) and invoke its methods on the client application. 

```
MyActor --- MyActor.Interfaces
         |
         +- MyActorService
         |
         +- MyActorClient
```

* **The interface project(\MyActor\MyActor.Interfaces).** This project contains the interface definition for the actor. Actor interfaces can be defined in any project with any name. The interface defines the actor contract that is shared by the actor implementation and the clients calling the actor. Because client projects may depend on it, it typically makes sense to define it in an assembly that is separate from the actor implementation.

* **The actor service project(\MyActor\MyActorService).** This project implements ASP.Net Core web service that is going to host the actor. It contains the implementation of the actor, MyActor.cs. An actor implementation is a class that derives from the base type Actor and implements the interfaces defined in the MyActor.Interfaces project. An actor class must also implement a constructor that accepts an ActorService instance and an ActorId and passes them to the base Actor class.

* **The actor client project(\MyActor\MyActorClient)** This project contains the implementation of the actor client which calls MyActor's method defined in Actor Interfaces.


## STEP 1 - Create Actor Interface

Actor interface defines the actor contract that is shared by the actor implementation and the clients calling the actor.

Actor interface is defined with the below requirements:

* Actor interface must inherit `Dapr.Actors.IActor` interface
* The return type of Actor method must be `Task` or `Task<object>`
* Actor method can have one argument at a maximum

### Create project and add dependencies

```bash
# Create Actor Interfaces
dotnet new classlib -o MyActor.Interfaces

cd MyActor.Interfaces

# Add Dapr.Actors nuget package. Please use the latest package version from nuget.org
dotnet add package Dapr.Actors -v 1.0.0-rc02
```


### Implement IMyActor Interface

Define IMyActor Interface and MyData data object.

```csharp
using Dapr.Actors;
using System.Threading.Tasks;

namespace MyActor.Interfaces
{
    public interface IMyActor : IActor
    {       
        Task<string> SetDataAsync(MyData data);
        Task<MyData> GetDataAsync();
        Task RegisterReminder();
        Task UnregisterReminder();
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

## STEP 2 - Create Actor Service

Dapr uses ASP.NET web service to host Actor service. This section will implement `IMyActor` actor interface and register Actor to Dapr Runtime.

### Create project and add dependencies

```bash
# Create ASP.Net Web service to host Dapr actor
dotnet new webapi -o MyActorService

cd MyActorService

# Add Dapr.Actors nuget package. Please use the latest package version from nuget.org
dotnet add package Dapr.Actors -v 1.0.0-rc02

# Add Dapr.Actors.AspNetCore nuget package. Please use the latest package version from nuget.org
dotnet add package Dapr.Actors.AspNetCore -v 1.0.0-rc02

# Add Actor Interface reference
dotnet add reference ../MyActor.Interfaces/MyActor.Interfaces.csproj
```

### Add Actor implementation

Implement IMyActor interface and derive from `Dapr.Actors.Actor` class. Following example shows how to use Actor Reminders as well. For Actors to use Reminders, it must derive from IRemindable. If you don't intend to use Reminder feature, you can skip implementing IRemindable and reminder specific methods which are shown in the code below.

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

#### Using an explicit actor type name

By default, the "type" of the actor as seen by clients is derived from the name of the actor implementation class. If desired, you can specify an explicit type name by attaching an `ActorAttribute` attribute to the actor implementation class.

```csharp
    [Actor(TypeName = "MyCustomActorTypeName")]
    internal class MyActor : Actor, IMyActor
    {
        // ...
    }
```

### Register Actor runtime with ASP.NET Core startup

The Actor runtime is configured through ASP.NET Core `Startup.cs`. 

The runtime uses the ASP.NET Core dependency injection system to register actor types and essential services. This integration is provided through the `AddActors(...)` method call in `ConfigureServices(...)`. Use the delegate passed to `AddActors(...)` to register actor types and configure actor runtime settings. You can register additional types for dependency injection inside `ConfigureServices(...)`. These will be available to be injected into the constructors of your Actor types.

Actors are implemented via HTTP calls with the Dapr runtime. This functionality is part of the application's HTTP processing pipeline and is registered inside `UseEndpoints(...)` inside `Configure(...)`.


```csharp
        // In Startup.cs
        public void ConfigureServices(IServiceCollection services)
        {
            // Register actor runtime with DI
            services.AddActors(options =>
            {
                // Register actor types and configure actor settings
                options.Actors.RegisterActor<MyActor>();
            });

            // Register additional services for use with actors
            services.AddSingleton<BankService>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                // Register actors handlers that interface with the Dapr runtime.
                endpoints.MapActorsHandlers();
            });
        }
```

### **Optional** - Override Default Actor Settings

Actor Settings are per app.  The settings described [here](https://docs.dapr.io/reference/api/actors_api/) are available on the options and can be modified as below.

The following code extends the previous section to do this.  Please note the values below are an **example** only.

```csharp

        // In Startup.cs
        public void ConfigureServices(IServiceCollection services)
        {
            // Register actor runtime with DI
            services.AddActors(options =>
            {
                // Register actor types and configure actor settings
                options.Actors.RegisterActor<MyActor>();
                
                options.ActorIdleTimeout = TimeSpan.FromMinutes(10);
                options.ActorScanInterval = TimeSpan.FromSeconds(35);
                options.DrainOngoingCallTimeout = TimeSpan.FromSeconds(35);
                options.DrainRebalancedActors = true;
            });

            // Register additional services for use with actors
            services.AddSingleton<BankService>();
        }
```

## STEP 3 - Add a client

Create a simple console app to call the actor service. Dapr SDK provides Actor Proxy client to invoke actor methods defined in Actor Interface.

### Create project and add dependencies

```bash
# Create Actor's Client
dotnet new console -o MyActorClient

cd MyActorClient

# Add Dapr.Actors nuget package. Please use the latest package version from nuget.org
dotnet add package Dapr.Actors -v 1.0.0-rc02

# Add Actor Interface reference
dotnet add reference ../MyActor.Interfaces/MyActor.Interfaces.csproj
```

### Invoke Actor method with Actor Service Remoting

We recommend to use the local proxy to actor instance because `ActorProxy.Create<IMyActor>(actorID, actorType)` returns strongly-typed actor instance to set up the remote procedure call.

```csharp
namespace MyActorClient
{
    using Dapr.Actors;
    using Dapr.Actors.Client;
    using MyActor.Interfaces;
    using System;
    using System.Threading.Tasks;

    ...
        static async Task InvokeActorMethodWithRemotingAsync()
        {
            var actorType = "MyActor";      // Registered Actor Type in Actor Service
            var actorID = new ActorId("1");

            // Create the local proxy by using the same interface that the service implements
            // By using this proxy, you can call strongly typed methods on the interface using Remoting.
            var proxy = ActorProxy.Create<IMyActor>(actorID, actorType);
            var response = await proxy.SetDataAsync(new MyData()
            {
                PropertyA = "ValueA",
                PropertyB = "ValueB",
            });
            Console.WriteLine(response);

            var savedData = await proxy.GetDataAsync();
            Console.WriteLine(savedData);
        }
    ...
}
```

### Invoke Actor method without Actor Service Remoting
You can invoke Actor methods without remoting (directly over http or using helper methods provided in ActorProxy), if Actor method accepts at-most one argument. Actor runtime will deserialize the incoming request body from client and use it as method argument to invoke the actor method.
When making non-remoting calls Actor method arguments and return types are serialized, deserialized as JSON.

`ActorProxy.Create(actorID, actorType)` returns ActorProxy instance and allow to use the raw http client to invoke the method defined in `IMyActor`.

```csharp
namespace MyActorClient
{
    using Dapr.Actors;
    using Dapr.Actors.Client;
    using MyActor.Interfaces;
    using System;
    using System.Threading.Tasks;

    ...
        static async Task InvokeActorMethodWithoutRemotingAsync()
        {
            var actorType = "MyActor";
            var actorID = new ActorId("1");

            // Create Actor Proxy instance to invoke the methods defined in the interface
            var proxy = ActorProxy.Create(actorID, actorType);
            // Need to specify the method name and response type explicitly
            var response = await proxy.InvokeMethodAsync<MyData, string>("SetDataAsync", new MyData()
            {
                PropertyA = "ValueA",
                PropertyB = "ValueB",
            });
            Console.WriteLine(response);

            var savedData = await proxy.InvokeMethodAsync<MyData>("GetDataAsync");
            Console.WriteLine(savedData);
        }
    ...
}
```

## Run Actor

In order to validate and debug actor service and client, we need to run actor services via Dapr CLI first.

1. Run Dapr Runtime via Dapr cli

   ```bash
   $ dapr run --app-id myapp --app-port 5000 --dapr-http-port 3500 dotnet run
   ```

   After executing MyActorService via Dapr runtime, make sure that application is discovered on port 5000 and actor connection is established successfully.

   ```bash
    INFO[0000] starting Dapr Runtime -- version  -- commit
    INFO[0000] log level set to: info
    INFO[0000] standalone mode configured
    INFO[0000] dapr id: myapp
    INFO[0000] loaded component statestore (state.redis)
    INFO[0000] application protocol: http. waiting on port 5000
    INFO[0000] application discovered on port 5000
    INFO[0000] application configuration loaded
    2019/08/27 14:42:06 redis: connecting to localhost:6379
    2019/08/27 14:42:06 redis: connected to localhost:6379 (localAddr: [::1]:53155, remAddr: [::1]:6379)
    INFO[0000] actor runtime started. actor idle timeout: 1h0m0s. actor scan interval: 30s
    INFO[0000] actors: starting connection attempt to placement service at localhost:50005
    INFO[0000] http server is running on port 3500
    INFO[0000] gRPC server is running on port 50001
    INFO[0000] dapr initialized. Status: Running. Init Elapsed 19.699438ms
    INFO[0000] actors: established connection to placement service at localhost:50005
    INFO[0000] actors: placement order received: lock
    INFO[0000] actors: placement order received: update
    INFO[0000] actors: placement tables updated
    INFO[0000] actors: placement order received: unlock
    ...
   ```

2. Run MyActorClient

   MyActorClient will console out if it calls actor hosted in MyActorService successfully.

   > If you specify the different Dapr runtime http port (default port: 3500), you need to set DAPR_HTTP_PORT environment variable before running the client.

   ```bash
   Success
   PropertyA: ValueA, PropertyB: ValueB
   ```
