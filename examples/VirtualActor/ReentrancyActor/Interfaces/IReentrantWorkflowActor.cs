// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License")
// ------------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapr.VirtualActors;

namespace ReentrancyActor.Interfaces;

/// <summary>
/// An actor that demonstrates reentrancy — the ability for an actor to be called
/// while it is already executing another method, within the same logical call chain.
/// </summary>
/// <remarks>
/// Without reentrancy enabled, an actor can only process one call at a time.
/// With reentrancy, calls from the same logical chain (identified by a reentrancy ID)
/// are allowed to proceed, preventing deadlocks in actor-to-actor call graphs.
/// </remarks>
public interface IReentrantWorkflowActor : IVirtualActor
{
    /// <summary>
    /// Starts a workflow step that internally calls back to this actor.
    /// Requires reentrancy to be enabled to avoid deadlock.
    /// </summary>
    Task<string> StartWorkflowStepAsync(string stepName, CancellationToken ct = default);

    /// <summary>
    /// A sub-step called recursively within the same logical chain.
    /// </summary>
    Task<string> ExecuteSubStepAsync(string stepName, int depth, CancellationToken ct = default);

    /// <summary>Gets the call history for inspection.</summary>
    Task<IReadOnlyList<string>> GetCallHistoryAsync(CancellationToken ct = default);
}
