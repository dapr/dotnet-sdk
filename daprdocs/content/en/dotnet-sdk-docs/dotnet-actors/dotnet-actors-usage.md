---
type: docs
title: "Author & run actors"
linkTitle: "Authoring actors"
weight: 200000
description: Learn all about authoring and running actors with the .NET SDK
---

## Author actors

### ActorHost

The `ActorHost`:

- Is a required constructor parameter of all actors
- Is provided by the runtime
- Must be passed to the base class constructor
- Contains all of the state that allows that actor instance to communicate with the runtime

```csharp
internal class MyActor : Actor, IMyActor, IRemindable
{
    public MyActor(ActorHost host) // Accept ActorHost in the constructor
        : base(host) // Pass ActorHost to the base class constructor
    {
    }
}
```

Since the `ActorHost` contains state unique to the actor, you don't need to pass the instance into other parts of your code. It's recommended only create your own instances of `ActorHost` in tests.

### Dependency injection

Actors support [dependency injection](https://docs.microsoft.com/aspnet/core/fundamentals/dependency-injection) of additional parameters into the constructor. Any other parameters you define will have their values satisfied from the dependency injection container.

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

An actor type should have a single `public` constructor. The actor infrastructure uses the [`ActivatorUtilities`](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection#constructor-injection-behavior) pattern for constructing actor instances.

You can register types with dependency injection in `Startup.cs` to make them available. Read more about [the different ways of registering your types](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?#service-registration-methods).

```csharp
// In Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    ...

    // Register additional types with dependency injection.
    services.AddSingleton<BankService>();
}
```

Each actor instance has its own dependency injection scope and remains in memory for some time after performing an operation. During that time, the dependency injection scope associated with the actor is also considered live. The scope will be released when the actor is deactivated.

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

When using this pattern, avoid creating many instances of **transient** services which implement `IDisposable`. Since the scope associated with an actor could be considered valid for a long time, you can accumulate many services in memory. See the [dependency injection guidelines](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines) for more information.

### IDisposable and actors

Actors can implement `IDisposable` or `IAsyncDisposable`. It's recommended that you rely on dependency injection for resource management rather than implementing dispose functionality in application code. Dispose support is provided in the rare case where it is truly necessary. 

### Logging

Inside an actor class, you have access to an `ILogger` instance through a property on the base `Actor` class. This instance is connected to the ASP.NET Core logging system and should be used for all logging inside an actor. Read more about [logging](https://docs.microsoft.com/en-us/dotnet/core/extensions/logging?tabs=command-line). You can configure a variety of different logging formats and output sinks.

Use _structured logging_ with _named placeholders_ like the example below:

```csharp
public Task<MyData> GetDataAsync()
{
    this.Logger.LogInformation("Getting state at {CurrentTime}", DateTime.UtcNow);
    return this.StateManager.GetStateAsync<MyData>("my_data");
}
```

When logging, avoid using format strings like: `$"Getting state at {DateTime.UtcNow}"`

Logging should use the [named placeholder syntax](https://docs.microsoft.com/dotnet/core/extensions/logging?tabs=command-line#log-message-template) which offers better performance and integration with logging systems.

### Using an explicit actor type name

By default, the _type_ of the actor, as seen by clients, is derived from the _name_ of the actor implementation class. The default name will be the class name (without namespace).

If desired, you can specify an explicit type name by attaching an `ActorAttribute` attribute to the actor implementation class.

```csharp
[Actor(TypeName = "MyCustomActorTypeName")]
internal class MyActor : Actor, IMyActor
{
    // ...
}
```

In the example above, the name will be `MyCustomActorTypeName`.

No change is needed to the code that registers the actor type with the runtime, providing the value via the attribute is all that is required.

## Host actors on the server

### Registering actors

Actor registration is part of `ConfigureServices` in `Startup.cs`. You can register services with dependency injection via the `ConfigureServices` method. Registering the set of actor types is part of the registration of actor services.

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

The actor runtime uses [System.Text.Json](https://docs.microsoft.com/dotnet/standard/serialization/system-text-json-overview) for:

- Serializing data to the state store
- Handling requests from the weakly-typed client

By default, the actor runtime uses settings based on [JsonSerializerDefaults.Web](https://docs.microsoft.com/dotnet/api/system.text.json.jsonserializerdefaults?view=net-5.0).

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

The `UseRouting` and `UseEndpoints` calls are necessary to configure routing. Configure actors as part of the pipeline by adding `MapActorsHandlers` inside the endpoint middleware.

This is a minimal example, it's valid for Actors functionality to existing alongside:

- Controllers
- Razor Pages
- Blazor
- gRPC Services
- Dapr pub/sub handler
- other endpoints such as health checks

### Problematic middleware

Certain middleware may interfere with the routing of Dapr requests to the actors handlers. In particular, the `UseHttpsRedirection` is problematic for Dapr's default configuration. Dapr sends requests over unencrypted HTTP by default, which the `UseHttpsRedirection` middleware will block. This middleware cannot be used with Dapr at this time.

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

## Next steps

Try the [Running and using virtual actors example]({{< ref dotnet-actors-howto.md >}}). 