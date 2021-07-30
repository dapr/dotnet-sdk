---
type: docs
title: "Dapr actor .NET usage guide"
linkTitle: "Actors client"
weight: 100000
description: Learn all about using the actor client with the .NET SDK
---

## Using the IActorProxyFactory

Inside of an `Actor` class or otherwise inside of an ASP.NET Core project you should use the `IActorProxyFactory` interface to create actor clients.

The `AddActors(...)` method will register actor services with ASP.NET Core dependency injection. 

- Outside of an actor instance, the `IActorProxyFactory` instance is available through dependency injection as a singleton service.
- Inside an actor instance, the `IActorProxyFactory` instance is available as a property (`this.ProxyFactory`).

The following is an example of creating a proxy inside an actor:

```csharp
public Task<MyData> GetDataAsync()
{
    var proxy = this.ProxyFactory.CreateActorProxy<IOtherActor>(ActorId.CreateRandom(), "OtherActor");
    await proxy.DoSomethingGreat();

    return this.StateManager.GetStateAsync<MyData>("my_data");
}
```

> 💡 For a non-dependency-injected application you can use the static methods on `ActorProxy`. These methods are error prone when you need to configure custom settings, and should be avoided when possible.

The guidance in this document will focus on `IActorProxyFactory`. `ActorProxy`'s static method functionality is identical except for the ability to manage configuration centrally.

## Identifying an actor

In order to communicate with an actor, you will need to know its type and id, and for a strongly-typed client one of its interfaces. All of the APIs on `IActorProxyFactory` will require an actor type and actor id.

- The actor type uniquely identifies the actor implementation across the whole application. 
- The actor id uniquely identifies an instance of that type.

If you do not have an actor id and want to communicate with a new instance, you can use `ActorId.CreateRandom()` to create a randomized id. Since the random id is a cryptographically strong identifier, the runtime will create a new actor instance when you interact with it.

You can use the type `ActorReference` to exchange an actor type and actor id with other actors as part of messages. 

## Two styles of actor client

The actor client supports two different styles of invocation: *strongly-typed* clients that use .NET interfaces and *weakly-typed* clients that use the `ActorProxy` class.

Since *strongly-typed* clients are based on .NET interfaces provide the typical benefits of strong-typing, however they do not work with non-.NET actors. You should use the *weakly-typed* client only when required for interop or other advanced reasons.

### Using a strongly-typed client

Use the `CreateActorProxy<>` method to create a strongly-typed client like the following example. `CreateActorProxy<>` requires an actor interface type, and will return an instance of that interface.

```csharp
// Create a proxy for IOtherActor to type OtherActor with a random id
var proxy = this.ProxyFactory.CreateActorProxy<IOtherActor>(ActorId.CreateRandom(), "OtherActor");

// Invoke a method defined by the interface to invoke the actor
//
// proxy is an implementation of IOtherActor so we can invoke its methods directly
await proxy.DoSomethingGreat();
```

### Using a weakly-typed client

Use the `Create` method to create a weakly-typed client like the following example. `Create` returns an instance of `ActorProxy`.

```csharp
// Create a proxy for type OtherActor with a random id
var proxy = this.ProxyFactory.Create(ActorId.CreateRandom(), "OtherActor");

// Invoke a method by name to invoke the actor
//
// proxy is an instance of ActorProxy.
await proxy.InvokeMethodAsync("DoSomethingGreat");
```

Since `ActorProxy` is a weakly-typed proxy you need to pass in the actor method name as a string.

You can also use `ActorProxy` to invoke methods with a request message and response message. Request and response messages will be serialized using the `System.Text.Json` serializer.

```csharp
// Create a proxy for type OtherActor with a random id
var proxy = this.ProxyFactory.Create(ActorId.CreateRandom(), "OtherActor");

// Invoke a method on the proxy to invoke the actor
//
// proxy is an instance of ActorProxy.
var request = new MyRequest() { Message = "Hi, it's me.", };
var response = await proxy.InvokeMethodAsync<MyRequest, MyResponse>("DoSomethingGreat", request);
```

When using a weakly-typed proxy, it is your responsibility to define the correct actor method names and message types. This is done for you when using a strongly-typed proxy since the names and types are part of the interface definition.
