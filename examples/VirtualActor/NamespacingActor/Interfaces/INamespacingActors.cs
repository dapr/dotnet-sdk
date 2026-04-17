// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License")
// ------------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapr.VirtualActors;

namespace NamespacingActor.Interfaces;

/// <summary>
/// A tenant-scoped account actor. The actor type name includes the tenant ID
/// to logically namespace actors across different tenants without requiring
/// separate Dapr components.
/// </summary>
public interface ITenantAccountActor : IVirtualActor
{
    /// <summary>Gets the current balance for this account.</summary>
    Task<decimal> GetBalanceAsync(CancellationToken ct = default);

    /// <summary>Deposits an amount into the account.</summary>
    Task DepositAsync(decimal amount, CancellationToken ct = default);

    /// <summary>Withdraws an amount from the account.</summary>
    Task<bool> WithdrawAsync(decimal amount, CancellationToken ct = default);
}

/// <summary>
/// A global leaderboard actor that aggregates data across all tenant accounts.
/// </summary>
public interface ILeaderboardActor : IVirtualActor
{
    /// <summary>Records a score for a player.</summary>
    Task RecordScoreAsync(string tenantId, string playerId, int score, CancellationToken ct = default);

    /// <summary>Gets the top N entries.</summary>
    Task<IReadOnlyList<LeaderboardEntry>> GetTopEntriesAsync(int count, CancellationToken ct = default);
}

/// <summary>Represents a leaderboard entry.</summary>
public sealed record LeaderboardEntry(string TenantId, string PlayerId, int Score);
