---
type: docs
title: "Dapr actor .NET usage guide"
linkTitle: "Authoring actors"
weight: 200000
description: Learn all about authoring and running actors with the .NET SDK
---

## Authoring actors

### ActorHost

The `ActorHost` is a required constructor parameter of all actors, and must be passed to the base class constructor.

```csharp
internal class MyActor : Actor, IMyActor, IRemindable
{
    public MyActor(ActorHost host) // Accept ActorHost in the constructor
        : base(host) // Pass ActorHost to the base class constructor
    {
    }
}
```

The `ActorHost` is provided by the runtime and contains all of the state that the allows that actor instance to communicate with the runtime. Since the `ActorHost` contains state unique to the actor, you should not pass the instance into other parts of your code. You should not create your own instances of `ActorHost` except in tests.

### Using dependency injection

Actors support [depenendency injection](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection) of additional parameters into the constructor. Any other parameters your define will have their values satisfied from the dependency injection container.

```csharp
internal class MyActor : Actor, IMyActor, IRemindable
{
    public MyActor(ActorHost host, BankService bank) // Accept BankService in the constructor
        : base(host)
    {
        ...
    }
}
```

An actor type should have a single `public` constructor. The actor infrastructure uses the [ActivatorUtilities](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection#constructor-injection-behavior) pattern for constructing actor instances.

You can register types with dependency injection in `Startup.cs` to make them available. You can read more about the different ways of registering your types [here](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?#service-registration-methods)

```csharp
// In Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    ...

    // Register additional types with dependency injection.
    services.AddSingleton<BankService>();
}
```

Each actor instance has its own dependency injection scope. Each actor remains in memory for some time after performing an operation, and during that time the dependency injection scope associated with the actor is also considered live. The scope will be releases when the actor is deactivated.

If an actor injects an `IServiceProvider` in the constructor, the actor will receive a reference to the `IServiceProvider` associated with its scope. The `IServiceProvider` can be used to resolve services dynamically in the future.

```csharp
internal class MyActor : Actor, IMyActor, IRemindable
{
    public MyActor(ActorHost host, IServiceProvider services) // Accept IServiceProvider in the constructor
        : base(host)
    {
        ...
    }
}
```

When using this pattern, take care to avoid creating many instances of **transient** services which implement `IDisposable`. Since the scope associated with an actor could be considered valid for a long time, it is possible to accumulate many services in memory. See the [dependency injection guidelines](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines) for more information.

### IDisposable and actors

Actors can implement `IDisposable` or `IAsyncDisposable`. It is recommended that you rely on dependency injection for resource management rather than implementing dispose functionality in application code. Dispose support is provided for the rare case where it is truly necessary. 

### Logging

Inside of an actor class you have access to an instance of `ILogger` through a property on the base `Actor` class. This instance is connected to the ASP.NET Core logging system, and should be used for all logging inside an actor. Read more about logging [here](https://docs.microsoft.com/en-us/dotnet/core/extensions/logging?tabs=command-line). You can configure a variety of different logging formats and output sinks.

You should use *structured logging* with *named placeholders* like the example below:

```csharp
public Task<MyData> GetDataAsync()
{
    this.Logger.LogInformation("Getting state at {CurrentTime}", DateTime.UtcNow);
    return this.StateManager.GetStateAsync<MyData>("my_data");
}
```

When logging, avoid using format strings like: `$"Getting state at {DateTime.UtcNow}"`

Logging should use the [named placeholder syntax](https://docs.microsoft.com/en-us/dotnet/core/extensions/logging?tabs=command-line#log-message-template) which is more performant and offers better integration with logging systems.

### Using an explicit actor type name

By default, the *type* of the actor as seen by clients is derived from the name of the actor implementation class. The default name will be the class name name (without namespace).

If desired, you can specify an explicit type name by attaching an `ActorAttribute` attribute to the actor implementation class.

```csharp
[Actor(TypeName = "MyCustomActorTypeName")]
internal class MyActor : Actor, IMyActor
{
    // ...
}
```

In the example above the name will be `MyCustomActorTypeName`.

No change is needed to the code that registers the actor type with the runtime, providing the value via the attribute is all that is required.

## Hosting actors on the server

### Registering actors

Actor registration is part `ConfigureServices` in `Startup.cs`. The `ConfigureServices` method is where services are registered with dependency injection, and registering the set of actor types is part of the registration of actor services.

Inside `ConfigureServices` you can:

- Register the actor runtime (`AddActors`)
- Register actor types (`options.Actors.RegisterActor<>`)
- Configure actor runtime settings `options`
- Register additional service types for dependency injection into actors (`services`)

```csharp
// In Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Register actor runtime with DI
    services.AddActors(options =>
    {
        // Register actor types and configure actor settings
        options.Actors.RegisterActor<MyActor>();
        
        // Configure default settings
        options.ActorIdleTimeout = TimeSpan.FromMinutes(10);
        options.ActorScanInterval = TimeSpan.FromSeconds(35);
        options.DrainOngoingCallTimeout = TimeSpan.FromSeconds(35);
        options.DrainRebalancedActors = true;
    });

    // Register additional services for use with actors
    services.AddSingleton<BankService>();
}
```

### Configuring JSON options

The actor runtime uses [System.Text.Json](https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-overview) for serializing data to the state store, and for handling requests from the weakly-typed client.

By default the actor runtime uses settings based on [JsonSerializerDefaults.Web](https://docs.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializerdefaults?view=net-5.0)

You can configure the `JsonSerializerOptions` as part of `ConfigureServices`:

```csharp
// In Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    services.AddActors(options =>
    {
        ...
        
        // Customize JSON options
        options.JsonSerializerOptions = ...
    });
}
```

### Actors and routing

The ASP.NET Core hosting support for actors uses the [endpoint routing](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/routing) system. The .NET SDK provides no support hosting actors with the legacy routing system from early ASP.NET Core releases.

Since actors uses endpoint routing, the actors HTTP handler is part of the middleware pipeline. The following is a minimal example of a `Configure` method setting up the middleware pipeline with actors.

```csharp
// in Startup.cs
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    if (env.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

    app.UseRouting();

    app.UseEndpoints(endpoints =>
    {
        // Register actors handlers that interface with the Dapr runtime.
        endpoints.MapActorsHandlers();
    });
}
```

The `UseRouting` and `UseEndpoints` calls are necessary to configure routing. Adding `MapActorsHandlers` inside the endpoint middleware is what configures actors as part of the pipeline.

This is a minimal example, it's valid for Actors functionality to existing alongside:

- Controllers
- Razor Pages
- Blazor
- gRPC Services
- Dapr pub/sub handler
- other endpoints such as health checks

### Problematic middleware

Certain middleware may interfere with the routing of Dapr requests to the actors handlers. In particular the `UseHttpsRedirection` is problematic for the default configuration of Dapr. Dapr will send requests over unencrypted HTTP by default, which will then be blocked by the `UseHttpsRedirection` middleware. This middleware cannot be used with Dapr at this time.

```csharp
// in Startup.cs
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    if (env.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

    // INVALID - this will block non-HTTPS requests
    app.UseHttpsRedirection();
    // INVALID - this will block non-HTTPS requests

    app.UseRouting();

    app.UseEndpoints(endpoints =>
    {
        // Register actors handlers that interface with the Dapr runtime.
        endpoints.MapActorsHandlers();
    });
}
```