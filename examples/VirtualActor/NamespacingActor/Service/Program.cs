// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License")
// ------------------------------------------------------------------------

// This example demonstrates the namespacing pattern for multi-tenant actor deployments.
//
// Instead of relying on a single actor type name, we register the same implementation
// under different namespaced names (one per tenant). Each namespace gets its own
// independent actor state partition.
//
// Actor type names registered:
//   - "Tenant_acme_AccountActor"
//   - "Tenant_globex_AccountActor"
//   - "LeaderboardActor"

using Dapr.VirtualActors.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NamespacingActor.Service;

var tenantIds = new[] { "acme", "globex" };

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddDaprVirtualActors(options =>
        {
            // Register the same implementation under a namespaced type name for each tenant.
            // Each tenant's actors are logically isolated by their type name.
            foreach (var tenantId in tenantIds)
            {
                var namespacedTypeName = $"Tenant_{tenantId}_AccountActor";
                options.RegisterActor<TenantAccountActor>(
                    factory: h => new TenantAccountActor(h),
                    actorTypeName: namespacedTypeName);
            }

            // The global leaderboard is a single, shared actor type
            options.RegisterActor<LeaderboardActor>(h => new LeaderboardActor(h));
        });
    })
    .Build();

await host.RunAsync();
