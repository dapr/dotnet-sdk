// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License")
// ------------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapr.VirtualActors;
using Dapr.VirtualActors.Runtime;
using Microsoft.Extensions.Logging;
using ReentrancyActor.Interfaces;

namespace ReentrancyActor.Service;

/// <summary>
/// Demonstrates actor reentrancy. This actor calls itself recursively via the proxy
/// factory — a pattern that requires reentrancy to be enabled to avoid deadlock.
/// </summary>
/// <remarks>
/// Enable reentrancy in DI setup:
/// <code>
/// services.AddDaprVirtualActors(options =>
/// {
///     options.Reentrancy.Enabled = true;
///     options.Reentrancy.MaxStackDepth = 32;
/// });
/// </code>
/// </remarks>
public sealed class ReentrantWorkflowActor(VirtualActorHost host)
    : VirtualActor(host), IReentrantWorkflowActor
{
    private const string HistoryKey = "call_history";
    private const int MaxDepth = 3;

    /// <inheritdoc />
    public async Task<string> StartWorkflowStepAsync(string stepName, CancellationToken ct = default)
    {
        Logger.LogInformation("Starting workflow step '{Step}'", stepName);
        await AppendHistoryAsync($"start:{stepName}", ct);

        // Call ourselves recursively — this requires reentrancy enabled
        var self = ProxyFactory.CreateProxy<IReentrantWorkflowActor>(Id, "ReentrantWorkflowActor");
        var result = await self.ExecuteSubStepAsync(stepName, depth: 0, ct);

        await AppendHistoryAsync($"complete:{stepName}", ct);
        return result;
    }

    /// <inheritdoc />
    public async Task<string> ExecuteSubStepAsync(string stepName, int depth, CancellationToken ct = default)
    {
        await AppendHistoryAsync($"sub:{stepName}:{depth}", ct);

        if (depth < MaxDepth)
        {
            var self = ProxyFactory.CreateProxy<IReentrantWorkflowActor>(Id, "ReentrantWorkflowActor");
            return await self.ExecuteSubStepAsync(stepName, depth + 1, ct);
        }

        return $"Completed '{stepName}' at depth {depth}";
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetCallHistoryAsync(CancellationToken ct = default)
    {
        var result = await StateManager.TryGetStateAsync<List<string>>(HistoryKey, ct);
        return result.HasValue ? result.Value : [];
    }

    private async Task AppendHistoryAsync(string entry, CancellationToken ct)
    {
        var result = await StateManager.TryGetStateAsync<List<string>>(HistoryKey, ct);
        var history = result.HasValue ? result.Value : [];
        history.Add(entry);
        await StateManager.SetStateAsync(HistoryKey, history, ct);
    }
}
