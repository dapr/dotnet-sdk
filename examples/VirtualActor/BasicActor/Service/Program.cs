// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License")
// ------------------------------------------------------------------------

using BasicActor.Service;
using Dapr.VirtualActors.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Build the host using Microsoft.Extensions.Hosting — no ASP.NET Core required.
// The source generator (Dapr.VirtualActors.Generators) automatically discovers
// GreetingActor and generates the DI registration. No manual RegisterActor call needed.

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Minimal setup: the generator creates an auto-registration hook
        // that is invoked here via VirtualActorAutoRegistration.ApplyDiscoveryHooks().
        services.AddDaprVirtualActors(options =>
        {
            // This registration is here for documentation purposes only.
            // With the source generator, this line is auto-generated.
            options.RegisterActor<GreetingActor>(h => new GreetingActor(h));

            // Optional: configure timeouts and reentrancy
            options.ActorIdleTimeout = TimeSpan.FromMinutes(10);
        });
    })
    .Build();

await host.RunAsync();
