// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License")
// ------------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using Dapr.VirtualActors;

namespace BasicActor.Interfaces;

/// <summary>
/// Defines the interface for a simple greeting actor.
/// All methods must return <see cref="Task"/> or <see cref="Task{T}"/>.
/// </summary>
public interface IGreetingActor : IVirtualActor
{
    /// <summary>Greets a person by name.</summary>
    Task<string> GreetAsync(string name, CancellationToken ct = default);

    /// <summary>Returns how many times this actor has greeted someone.</summary>
    Task<int> GetGreetingCountAsync(CancellationToken ct = default);

    /// <summary>Resets the greeting counter.</summary>
    Task ResetCounterAsync(CancellationToken ct = default);
}
