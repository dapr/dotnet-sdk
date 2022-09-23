---
type: docs
title: "The IActorProxyFactory interface"
linkTitle: "Actors client"
weight: 100000
description: Learn how to create actor clients with the IActorProxyFactory interface
---

Inside of an `Actor` class or an ASP.NET Core project, the `IActorProxyFactory` interface is recommended to create actor clients.

The `AddActors(...)` method will register actor services with ASP.NET Core dependency injection.

- **Outside of an actor instance:** The `IActorProxyFactory` instance is available through dependency injection as a singleton service.
- **Inside an actor instance:** The `IActorProxyFactory` instance is available as a property (`this.ProxyFactory`).

The following is an example of creating a proxy inside an actor:

```csharp
public Task<MyData> GetDataAsync()
{
    var proxy = this.ProxyFactory.CreateActorProxy<IOtherActor>(ActorId.CreateRandom(), "OtherActor");
    await proxy.DoSomethingGreat();

    return this.StateManager.GetStateAsync<MyData>("my_data");
}
```

In this guide, you will learn how to use `IActorProxyFactory`. 

{{% alert title="Tip" color="primary" %}}
For a non-dependency-injected application, you can use the static methods on `ActorProxy`. Since the `ActorProxy` methods are error prone, try to avoid using them when configuring custom settings.
{{% /alert %}}

## Identifying an actor

All of the APIs on `IActorProxyFactory` will require an actor _type_ and actor _id_ to communicate with an actor. For strongly-typed clients, you also need one of its interfaces.

- **Actor type** uniquely identifies the actor implementation across the whole application. 
- **Actor id** uniquely identifies an instance of that type.

If you don't have an actor `id` and want to communicate with a new instance, create a random id with `ActorId.CreateRandom()`. Since the random id is a cryptographically strong identifier, the runtime will create a new actor instance when you interact with it.

You can use the type `ActorReference` to exchange an actor type and actor id with other actors as part of messages.

## Two styles of actor client

The actor client supports two different styles of invocation: 

| Actor client style | Description |
| ------------------ | ----------- |
| Strongly-typed | Strongly-typed clients are based on .NET interfaces and provide the typical benefits of strong-typing. They don't work with non-.NET actors. |
| Weakly-typed | Weakly-typed clients use the `ActorProxy` class. It is recommended to use these only when required for interop or other advanced reasons. |

### Using a strongly-typed client

The following example uses the `CreateActorProxy<>` method to create a strongly-typed client. `CreateActorProxy<>` requires an actor interface type, and will return an instance of that interface.

```csharp
// Create a proxy for IOtherActor to type OtherActor with a random id
var proxy = this.ProxyFactory.CreateActorProxy<IOtherActor>(ActorId.CreateRandom(), "OtherActor");

// Invoke a method defined by the interface to invoke the actor
//
// proxy is an implementation of IOtherActor so we can invoke its methods directly
await proxy.DoSomethingGreat();
```

### Using a weakly-typed client

The following example uses the `Create` method to create a weakly-typed client. `Create` returns an instance of `ActorProxy`.

```csharp
// Create a proxy for type OtherActor with a random id
var proxy = this.ProxyFactory.Create(ActorId.CreateRandom(), "OtherActor");

// Invoke a method by name to invoke the actor
//
// proxy is an instance of ActorProxy.
await proxy.InvokeMethodAsync("DoSomethingGreat");
```

Since `ActorProxy` is a weakly-typed proxy, you need to pass in the actor method name as a string.

You can also use `ActorProxy` to invoke methods with both a request and a response message. Request and response messages will be serialized using the `System.Text.Json` serializer.

```csharp
// Create a proxy for type OtherActor with a random id
var proxy = this.ProxyFactory.Create(ActorId.CreateRandom(), "OtherActor");

// Invoke a method on the proxy to invoke the actor
//
// proxy is an instance of ActorProxy.
var request = new MyRequest() { Message = "Hi, it's me.", };
var response = await proxy.InvokeMethodAsync<MyRequest, MyResponse>("DoSomethingGreat", request);
```

When using a weakly-typed proxy, you _must_ proactively define the correct actor method names and message types. When using a strongly-typed proxy, these names and types are defined for you as part of the interface definition.

### Actor method invocation exception details

The actor method invocation exception details are surfaced to the caller and the callee, providing an entry point to track down the issue. Exception details include:
 - Method name
 - Line number
 - Exception type
 - UUID 
 
You use the UUID to match the exception on the caller and callee side. Below is an example of exception details:
```
Dapr.Actors.ActorMethodInvocationException: Remote Actor Method Exception, DETAILS: Exception: NotImplementedException, Method Name: ExceptionExample, Line Number: 14, Exception uuid: d291a006-84d5-42c4-b39e-d6300e9ac38b
```

## Next steps

[Learn how to author and run actors with `ActorHost`]({{< ref dotnet-actors-usage.md >}}).