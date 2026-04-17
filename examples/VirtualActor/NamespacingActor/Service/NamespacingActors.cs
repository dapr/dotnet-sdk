// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License")
// ------------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapr.VirtualActors.Runtime;
using NamespacingActor.Interfaces;

namespace NamespacingActor.Service;

/// <summary>
/// A tenant-scoped account actor registered with a namespaced type name.
/// </summary>
/// <remarks>
/// <para>
/// The actor type name is registered as <c>"Tenant_{tenantId}_AccountActor"</c>
/// at startup, allowing multiple logical actor namespaces to coexist within the
/// same Dapr application without conflicts.
/// </para>
/// <para>
/// This pattern is useful for multi-tenant SaaS applications where you want
/// per-tenant actor namespaces with independent state stores.
/// </para>
/// </remarks>
public sealed class TenantAccountActor(VirtualActorHost host)
    : VirtualActor(host), ITenantAccountActor
{
    private const string BalanceKey = "balance";

    /// <inheritdoc />
    public async Task<decimal> GetBalanceAsync(CancellationToken ct = default)
    {
        var result = await StateManager.TryGetStateAsync<decimal>(BalanceKey, ct);
        return result.HasValue ? result.Value : 0m;
    }

    /// <inheritdoc />
    public async Task DepositAsync(decimal amount, CancellationToken ct = default)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        var balance = await GetBalanceAsync(ct);
        await StateManager.SetStateAsync(BalanceKey, balance + amount, ct);
    }

    /// <inheritdoc />
    public async Task<bool> WithdrawAsync(decimal amount, CancellationToken ct = default)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        var balance = await GetBalanceAsync(ct);
        if (balance < amount) return false;

        await StateManager.SetStateAsync(BalanceKey, balance - amount, ct);
        return true;
    }
}

/// <summary>
/// A global leaderboard actor used to aggregate scores across all tenants.
/// </summary>
public sealed class LeaderboardActor(VirtualActorHost host)
    : VirtualActor(host), ILeaderboardActor
{
    private const string EntriesKey = "entries";

    /// <inheritdoc />
    public async Task RecordScoreAsync(string tenantId, string playerId, int score, CancellationToken ct = default)
    {
        var result = await StateManager.TryGetStateAsync<List<LeaderboardEntry>>(EntriesKey, ct);
        var entries = result.HasValue ? result.Value : [];

        entries.RemoveAll(e => e.TenantId == tenantId && e.PlayerId == playerId);
        entries.Add(new LeaderboardEntry(tenantId, playerId, score));
        entries.Sort((a, b) => b.Score.CompareTo(a.Score));

        await StateManager.SetStateAsync(EntriesKey, entries, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LeaderboardEntry>> GetTopEntriesAsync(int count, CancellationToken ct = default)
    {
        var result = await StateManager.TryGetStateAsync<List<LeaderboardEntry>>(EntriesKey, ct);
        var entries = result.HasValue ? result.Value : [];
        return entries.Take(count).ToList();
    }
}
