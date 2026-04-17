// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License")
// ------------------------------------------------------------------------

using Dapr.VirtualActors.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReentrancyActor.Service;

// Reentrancy must be explicitly enabled. Without it, actor-to-actor calls
// within the same logical call chain will deadlock.

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddDaprVirtualActors(options =>
        {
            options.RegisterActor<ReentrantWorkflowActor>(h => new ReentrantWorkflowActor(h));

            // Enable reentrancy — required for recursive/chained actor calls
            options.Reentrancy.Enabled = true;
            options.Reentrancy.MaxStackDepth = 32;
        });
    })
    .Build();

await host.RunAsync();
