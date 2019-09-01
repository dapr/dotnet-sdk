# Getting started with Actions Actor

## Prerequistes
* [.Net Core SDK 2.2](https://dotnet.microsoft.com/download)
* [Actions CLI](https://github.com/actionscore/cli)
* [Actions DotNet SDK](https://github.com/actionscore/dotnet-sdk)

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

* Actor interface must inherit `Microsoft.Actions.Actors.IActor` interface
* The return type of Actor method must be `Task` or `Task<object>`
* Actor method can have one argument at a maximum

### Create project and add dependencies

```bash
# Create Actor Interfaces
dotnet new classlib -o MyActor.Interfaces

# Add Microsoft.Actions.Actors nuget package
dotnet add package Microsoft.Actions.Actors -v 0.3.0-preview1 -s ~/tmp/nugets/
```

### Implement IMyActor Interface

Define IMyActor Interface and MyData data object.

```csharp
using Microsoft.Actions.Actors;
using System.Threading.Tasks;

namespace MyActor.Interfaces
{
    public interface IMyActor : IActor
    {
        // Return Type must be `Task` or Task<T>.
        // Arguments and return type must be Datacontract serializable when making actor method calls using Remoting.
        Task<string> SetMyDataAsync(MyData data);
        Task<MyData> GetMyDataAsync();
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

Actions uses ASP.NET web service to host Actor service. This section will implement `IMyActor` actor interface and register Actor to Actions Runtime.

### Create project and add dependencies

```bash
# Create ASP.Net Web service to host Actions actor
dotnet new webapi -o MyActor

cd MyActor

# Add Microsoft.Actions.Actors nuget package
dotnet add package Microsoft.Actions.Actors -v 0.3.0-preview1 -s ~/tmp/nugets/

# Add Microsoft.Actions.Actors.AspNetCore nuget package
dotnet add package Microsoft.Actions.Actors.AspNetCore -v 0.3.0-preview1 -s ~/tmp/nugets/

# Add Actor Interface reference
dotnet add reference ../MyActor.Interfaces/MyActor.Interfaces.csproj
```

### Add Actor implementation

Implement IMyActor interface and derive from `Microsoft.Actions.Actors.Actor` class. Following example shows how to use Actor Reminders as well. For Actors to use Reminders, it must derive from IRemindable. If you don't intend to use Reminder feature, you can skip implementing IRemindable and reminder specific methods which are shown in the code below.

```csharp
using Microsoft.Actions.Actors;
using Microsoft.Actions.Actors.Runtime;
using MyActor.Interfaces;
using System;
using System.Threading.Tasks;

namespace MyActorService
{
    internal class MyActor : Actor, IMyActor, IRemindable
    {
        /// <summary>
        /// Initializes a new instance of MyActor
        /// </summary>
        /// <param name="actorService">The Microsoft.Actions.Actors.Runtime.ActorService that will host this actor instance.</param>
        /// <param name="actorId">The Microsoft.Actions.Actors.ActorId for this actor instance.</param>
        public MyActor(ActorService actorService, ActorId actorId)
            : base(actorService, actorId)
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
        public async Task<string> SetMyDataAsync(MyData data)
        {
            // Data is saved to configured state store implicitly after each method execution by Actor's runtime.
            // Data can also be saved explicitly by calling this.StateManager.SaveStateAsync();
            // State to be saved must be DataContract serialziable.
            await this.StateManager.SetStateAsync<MyData>(
                "my_data",  // state name
                data);      // data saved for the named state "my_data"

            return "Success";
        }

        /// <summary>
        /// Get MyData from actor's private state store
        /// </summary>
        /// <return>the user-defined MyData which is stored into state store as "my_data" state</return>
        public Task<MyData> GetMyDataAsync()
        {
            // Gets state from the state store.
            return this.StateManager.GetStateAsync<MyData>("my_data");
        }

        /// <summary>
        /// Register MyReminder reminder with the actor
        /// </summary>
        public async Task RegisterReminder()
        {
            this.reminder =  await this.RegisterReminderAsync(
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
                this.OnTimerCallBack,       // Timer callback
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
        private Task OnTimerCallBack(object data)
        {
            Console.WriteLine("OnTimerCallBack is called!");
            return Task.CompletedTask;
        }
    }
}
```

### Register Actor to Actions Runtime

Register `MyActor` actor type to actor runtime and set the localhost port (`https://localhost:3000`) which Actions Runtime can call Actor through.

```csharp
        private const int AppChannelHttpPort = 3000;

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseActionsActors(actorRuntime =>
                {
                    // Register MyActor actor type
                    actorRuntime.RegisterActor<MyActor>();
                }
                )
                .UseUrls($"http://localhost:{AppChannelHttpPort}/");
```

## STEP 3 - Add a client

Create a simple console app to call the actor service. Actions SDK provides Actor Proxy client to invoke actor methods defined in Actor Interface.

### Create project and add dependencies

```bash
# Create Actor's Client
dotnet new console -o MyActorClient

cd MyActorClient

# Add Microsoft.Actions.Actors nuget package
dotnet add package Microsoft.Actions.Actors -v 0.3.0-preview1 -s ~/tmp/nugets/

# Add Actor Interface reference
dotnet add reference ../MyActor.Interfaces/MyActor.Interfaces.csproj
```

### Invoke Actor method with Actor Service Remoting

We recommend to use the local proxy to actor instance because `ActorProxy.Create<IMyActor>(actorID, actorType)` returns strongly-typed actor instance to set up the remote procedure call.

```csharp
namespace MyActorClient
{
    using Microsoft.Actions.Actors;
    using Microsoft.Actions.Actors.Client;
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
            var response = await proxy.SetMyDataAsync(new MyData()
            {
                PropertyA = "ValueA",
                PropertyB = "ValueB",
            });
            Console.WriteLine(response);

            var savedData = await proxy.GetMyDataAsync();
            Console.WriteLine(savedData);
        }
    ...
}
```

### Invoke Actor method without Actor Service Remoting
You can invoke Actor methods without remoting (directly over http or using helper methods provided in ActorProxy), if Actor method accepts at-most one argument. Actor runtime will deserialize the incoming request body from client and use it as method argument to invoke the actor method.
When making non-remoting calls Actor method arguments and return types are serialized, deserialized as json.

`ActorProxy.Create(actorID, actorType)` returns ActorProxy instance and allow to use the raw http client to invoke the method defined in `IMyActor`.

```csharp
namespace MyActorClient
{
    using Microsoft.Actions.Actors;
    using Microsoft.Actions.Actors.Client;
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
            var response = await proxy.InvokeAsync<string>("SetMyDataAsync", new MyData()
            {
                PropertyA = "ValueA",
                PropertyB = "ValueB",
            });
            Console.WriteLine(response);

            var savedData = await proxy.InvokeAsync<MyData>("GetMyDataAsync");
            Console.WriteLine(savedData);
        }
    ...
}
```

## Run Actor

In order to validate and debug actor service and client, we need to run actor services via actions cli first.

1. Run Actions Runtime via Actions cli

   ```bash
   $ actions run --app-id myactions --app-port 3000 dotnet MyActorService.dll
   ```

   After executing MyActorService via actions runtime, make sure that application is discovered on port 3000 and actor connection is established successfully.

   ```bash
    INFO[0000] starting Actions Runtime -- version  -- commit
    INFO[0000] log level set to: info
    INFO[0000] standalone mode configured
    INFO[0000] action id: myactions
    INFO[0000] loaded component statestore (state.redis)
    INFO[0000] application protocol: http. waiting on port 3000
    INFO[0000] application discovered on port 3000
    INFO[0000] application configuration loaded
    2019/08/27 14:42:06 redis: connecting to localhost:6379
    2019/08/27 14:42:06 redis: connected to localhost:6379 (localAddr: [::1]:53155, remAddr: [::1]:6379)
    INFO[0000] actor runtime started. actor idle timeout: 1h0m0s. actor scan interval: 30s
    INFO[0000] actors: starting connection attempt to placement service at localhost:50005
    INFO[0000] http server is running on port 3500
    INFO[0000] gRPC server is running on port 50001
    INFO[0000] actions initialized. Status: Running. Init Elapsed 19.699438ms
    INFO[0000] actors: established connection to placement service at localhost:50005
    INFO[0000] actors: placement order received: lock
    INFO[0000] actors: placement order received: update
    INFO[0000] actors: placement tables updated
    INFO[0000] actors: placement order received: unlock
    ...
   ```

2. Run MyActorClient

   MyActorClient will console out if it calls actor hosted in MyActorService successfully.

   > If you specify the different actions runtime http port (default port: 3500), you need to set ACTIONS_PORT environment variable before running the client.

   ```bash
   Success
   PropertyA: ValueA, PropertyB: ValueB
   ```
