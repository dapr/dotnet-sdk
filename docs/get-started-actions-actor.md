# Getting started with Actions Actor

## Prerequistes
* [.Net Core SDK 2.2](https://dotnet.microsoft.com/download)
* [Actions CLI](https://github.com/actionscore/cli/releases)
* [Actions DotNet SDK](https://github.com/actionscore/cs-sdk/packages)
  - Download nuget file from [Release]() to local disk (e.g. ~/tmp/nugets)

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
dotnet add package Microsoft.Actions.Actors -v 1.0.0-preview001 -s ~/tmp/nugets/
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
        // Return type must be `Task<string>`
        // string ProcessStringData(string data);

        // Disallow 1+ arguments
        // Task<string> ProcessStringData(string data, string data2);

        Task<string> ProcessData(MyData data);
        Task<string> ProcessStringData(string data);
        Task<MyData> Echo(MyData data);
        Task<MyData> GetData();
        Task NoReturnTypeNoArg();
        Task ThrowException();
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
dotnet add package Microsoft.Actions.Actors -v 1.0.0-preview001 -s ~/tmp/nugets/

# Add Microsoft.Actions.Actors.AspNetCore nuget package
dotnet add package Microsoft.Actions.Actors.AspNetCore -v 1.0.0-preview001 -s ~/tmp/nugets/

# Add Actor Interface reference
dotnet add reference ../MyActor.Interfaces/MyActor.Interfaces.csproj
```

### Add Actor implementation

Implement IMyActor interface and derive from `Microsoft.Actions.Actors.Actor` class.

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
        private IActorReminder reminder;

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
            Console.WriteLine($"Dectivating actor id: {this.Id}");
            return Task.CompletedTask;
        }

        public MyActor(ActorService service, ActorId actorId) : base(service, actorId)
        {
            // Actor intiailization
        }

        public async Task<string> ProcessData(MyData data)
        {
            Console.WriteLine($"This is Actor id {this.Id}  with data {data.ToString()}");
            await this.StateManager.SetStateAsync<MyData>("my_data", data);
            return "Success";
        }

        public Task<MyData> Echo(MyData data)
        {
            return Task.FromResult(data);
        }

        public Task<MyData> GetData()
        {
            return this.StateManager.GetStateAsync<MyData>("my_data");
        }

        public Task<string> ProcessStringData(string data)
        {
            Console.WriteLine($"This is Actor id {this.Id}  with data {data}");
            return Task.FromResult("Success");
        }

        public Task NoReturnTypeNoArg()
        {
            return Task.CompletedTask;
        }

        public Task ThrowException()
        {
            throw new NotImplementedException();
        }

        public async Task RegisterReminder()
        {
            this.reminder =  await this.RegisterReminderAsync("Test", null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        public Task UnregisterReminder()
        {
            return this.UnregisterReminderAsync("Test");
        }

        public Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        {
            return Task.CompletedTask;
        }

        public Task RegisterTimer()
        {
            return this.RegisterTimerAsync("Test", this.TimerCallBack, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        public Task UnregisterTimer()
        {
            return this.UnregisterTimerAsync("Test");
        }

        private Task TimerCallBack(object data)
        {
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
dotnet add package Microsoft.Actions.Actors -v 1.0.0-preview001 -s ~/tmp/nugets/

# Add Actor Interface reference
dotnet add reference ../MyActor.Interfaces/MyActor.Interfaces.csproj
```

### Invoke Actor method via a proxy to actor object

We highly recommend to use the proxy to actor object because `ActorProxy.Create<IMyActor>(actorID, actorType)` returns the actual actor object and allows to call the methods in actor object like a normal RPC call.

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
            var actorType = "MyActor"; // Registered Actor Type in Actor Service
            var actorID = new ActorId("1");

            // Create the proxy to actor object: IMyActor
            var proxy = ActorProxy.Create<IMyActor>(actorID, actorType);

            // Invoke actor methods like normal method call
            var echoResult = await proxy.Echo(new MyData()
            {
                PropertyA = "ValueA",
                PropertyB = "ValueB",
            });

            await proxy.RegisterTimer();

            Console.WriteLine(echoResult);
        }
    ...
}
```

### Invoke Actor method with ActorProxy client

`ActorProxy.Create(actorID, actorType)` returns ActorProxy and allow to use the raw http client to invoke the method defined in `IMyActor`.


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
            var actorType = "MyActor"; // Registered Actor Type in Actor Service
            var actorID = new ActorId("1");

            // Create Actor Proxy client: ActorProxy
            var proxy = ActorProxy.Create(actorID, actorType);

            // Invoke actor methods with raw http call
            var echoResult = await proxy.InvokeAsync<MyData>("Echo", new MyData()
            {
                PropertyA = "ValueA",
                PropertyB = "ValueB",
            });

            await proxy.InvokeAsync("RegisterTimer");

            Console.WriteLine(echoResult);
        }
    ...
}
```

## Run and debug Actor

In order to validate and debug actor service and client, we need to run actor services via actions cli first.

1. Run Actions Runtime via Actions cli

   ```bash
   $ actions run --app-id myactions --app-port 3000
   ```

   Actions runtime will wait until app's http port is available

   ```
   INFO[0000] starting Actions Runtime -- version  -- commit
   INFO[0000] log level set to: info
   INFO[0000] standalone mode configured
   INFO[0000] action id: myactions
   INFO[0000] loaded component statestore (state.redis)
   INFO[0000] application protocol: http. waiting on port 3000
   ```

2. Run MyActorService

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
   ```

3. Run MyActorClient

   Print IMyActor.Echo() result if MyActorClient calls actor hosted in MyActorService successfully.

   ```bash
   PropertyA: ValueA, PropertyB: ValueB
   ```
