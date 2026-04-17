// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License")
// ------------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using BasicActor.Interfaces;
using Dapr.VirtualActors.Runtime;
using Microsoft.Extensions.Logging;

namespace BasicActor.Service;

/// <summary>
/// A simple greeting actor that demonstrates the Dapr VirtualActors DI-first API.
/// </summary>
/// <remarks>
/// <para>
/// Key differences from the classic Dapr.Actors approach:
/// - No static builder: use <c>services.AddDaprVirtualActors()</c>
/// - No manual <c>app.MapActorsHandlers()</c> required
/// - Source generator auto-discovers and registers this actor at build time
/// - Persistent gRPC connection replaces HTTP callbacks
/// </para>
/// </remarks>
public sealed class GreetingActor(VirtualActorHost host) : VirtualActor(host), IGreetingActor
{
    private const string CounterStateKey = "greeting_count";

    /// <inheritdoc />
    public async Task<string> GreetAsync(string name, CancellationToken ct = default)
    {
        var count = await GetOrDefaultCountAsync(ct);
        count++;
        await StateManager.SetStateAsync(CounterStateKey, count, ct);

        var greeting = $"Hello, {name}! You are greeting #{count}.";
        Logger.LogInformation("Greeted '{Name}' (count={Count})", name, count);
        return greeting;
    }

    /// <inheritdoc />
    public async Task<int> GetGreetingCountAsync(CancellationToken ct = default) =>
        await GetOrDefaultCountAsync(ct);

    /// <inheritdoc />
    public async Task ResetCounterAsync(CancellationToken ct = default)
    {
        await StateManager.SetStateAsync(CounterStateKey, 0, ct);
        Logger.LogInformation("Counter reset");
    }

    /// <inheritdoc />
    protected override Task OnActivateAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("GreetingActor {Id} activated", Id.GetId());
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override Task OnDeactivateAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("GreetingActor {Id} deactivated", Id.GetId());
        return Task.CompletedTask;
    }

    private async Task<int> GetOrDefaultCountAsync(CancellationToken ct)
    {
        var result = await StateManager.TryGetStateAsync<int>(CounterStateKey, ct);
        return result.HasValue ? result.Value : 0;
    }
}
