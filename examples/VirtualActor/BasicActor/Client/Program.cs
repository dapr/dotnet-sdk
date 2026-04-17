// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License")
// ------------------------------------------------------------------------

// This client demonstrates calling a VirtualActor via the DI-based proxy factory.
// In a real application, inject IVirtualActorProxyFactory into your services.

using BasicActor.Interfaces;
using Dapr.VirtualActors;
using Dapr.VirtualActors.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddDaprVirtualActors();
    })
    .Build();

await host.StartAsync();

var proxyFactory = host.Services.GetRequiredService<IVirtualActorProxyFactory>();

// Create a proxy to a GreetingActor with a specific actor ID.
// Actor IDs uniquely identify actor instances within a type.
var actorId = new VirtualActorId("alice");
var greetingActor = proxyFactory.CreateProxy<IGreetingActor>(actorId, "GreetingActor");

Console.WriteLine("Calling GreetAsync...");
var greeting = await greetingActor.GreetAsync("Alice");
Console.WriteLine(greeting);

var count = await greetingActor.GetGreetingCountAsync();
Console.WriteLine($"Greeting count: {count}");

// Each distinct actor ID is a separate actor instance with its own state
var bobId = new VirtualActorId("bob");
var bobActor = proxyFactory.CreateProxy<IGreetingActor>(bobId, "GreetingActor");
var bobGreeting = await bobActor.GreetAsync("Bob");
Console.WriteLine(bobGreeting);

await host.StopAsync();
