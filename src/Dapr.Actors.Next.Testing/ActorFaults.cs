// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using Dapr.Actors.Next.Core.State;

namespace Dapr.Actors.Next.Testing;

/// <summary>
/// Controls deterministic fault injection for actor tests.
/// </summary>
public sealed class ActorFaults : IActorStateFaultInjector
{
    private readonly Queue<StateWriteFault> stateWriteFaults = [];
    private readonly Queue<MigrationFault> migrationFaults = [];
    private readonly Queue<UpcastHopFault> upcastHopFaults = [];
    private readonly Queue<InvocationFault> invocationFaults = [];
    private readonly object syncRoot = new();

    /// <summary>
    /// Fails the next state write for the given state type.
    /// </summary>
    public void FailNextStateWrite<T>(bool transient = true, string? stateName = null) =>
        FailNextStateWrite(typeof(T), transient, stateName);

    /// <summary>
    /// Fails the next state write for the given state type.
    /// </summary>
    public void FailNextStateWrite(Type stateType, bool transient = true, string? stateName = null)
    {
        ArgumentNullException.ThrowIfNull(stateType);
        lock (syncRoot)
        {
            stateWriteFaults.Enqueue(new StateWriteFault(stateType, transient, stateName));
        }
    }

    /// <summary>
    /// Fails the next migrating read targeting the given state type.
    /// </summary>
    public void FailNextMigration<T>(bool transient = true, string? stateName = null) =>
        FailNextMigration(typeof(T), transient, stateName);

    /// <summary>
    /// Fails the next migrating read targeting the given state type.
    /// </summary>
    public void FailNextMigration(Type targetStateType, bool transient = true, string? stateName = null)
    {
        ArgumentNullException.ThrowIfNull(targetStateType);
        lock (syncRoot)
        {
            migrationFaults.Enqueue(new MigrationFault(targetStateType, transient, stateName));
        }
    }

    /// <summary>
    /// Fails the next upcast hop between the given state types.
    /// </summary>
    public void FailNextUpcastHop<TFrom, TTo>(bool transient = true, string? stateName = null) =>
        FailNextUpcastHop(typeof(TFrom), typeof(TTo), transient, stateName);

    /// <summary>
    /// Fails the next upcast hop between the given state types.
    /// </summary>
    public void FailNextUpcastHop(Type fromStateType, Type toStateType, bool transient = true, string? stateName = null)
    {
        ArgumentNullException.ThrowIfNull(fromStateType);
        ArgumentNullException.ThrowIfNull(toStateType);
        lock (syncRoot)
        {
            upcastHopFaults.Enqueue(new UpcastHopFault(fromStateType, toStateType, transient, stateName));
        }
    }

    /// <summary>
    /// Fails the next actor invocation at the in-memory invocation boundary.
    /// </summary>
    public void FailNextInvocation(bool transient = true, string? actorType = null, string? methodName = null)
    {
        lock (syncRoot)
        {
            invocationFaults.Enqueue(new InvocationFault(transient, actorType, methodName));
        }
    }

    /// <inheritdoc />
    public ValueTask BeforeWriteAsync(Type stateType, string actorType, string actorId, string stateName, CancellationToken cancellationToken = default)
    {
        StateWriteFault? fault = null;
        lock (syncRoot)
        {
            fault = DequeueFirstMatch(
                stateWriteFaults,
                next => next.StateType == stateType
                    && (next.StateName is null || string.Equals(next.StateName, stateName, StringComparison.Ordinal)));
        }

        if (fault is not null)
        {
            throw CreateException("Injected state write failure.", fault.Transient);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask BeforeMigrationAsync(Type targetStateType, string actorType, string actorId, string stateName, CancellationToken cancellationToken = default)
    {
        MigrationFault? fault = null;
        lock (syncRoot)
        {
            fault = DequeueFirstMatch(
                migrationFaults,
                next => next.TargetStateType == targetStateType
                    && (next.StateName is null || string.Equals(next.StateName, stateName, StringComparison.Ordinal)));
        }

        if (fault is not null)
        {
            throw CreateException("Injected actor state migration failure.", fault.Transient);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask BeforeUpcastHopAsync(Type fromStateType, Type toStateType, string actorType, string actorId, string stateName, CancellationToken cancellationToken = default)
    {
        UpcastHopFault? fault = null;
        lock (syncRoot)
        {
            fault = DequeueFirstMatch(
                upcastHopFaults,
                next => next.FromStateType == fromStateType
                    && next.ToStateType == toStateType
                    && (next.StateName is null || string.Equals(next.StateName, stateName, StringComparison.Ordinal)));
        }

        if (fault is not null)
        {
            throw CreateException("Injected actor state upcast-hop failure.", fault.Transient);
        }

        return ValueTask.CompletedTask;
    }

    internal void BeforeInvocation(string actorType, string methodName)
    {
        InvocationFault? fault = null;
        lock (syncRoot)
        {
            fault = DequeueFirstMatch(
                invocationFaults,
                next => (next.ActorType is null || string.Equals(next.ActorType, actorType, StringComparison.Ordinal))
                    && (next.MethodName is null || string.Equals(next.MethodName, methodName, StringComparison.Ordinal)));
        }

        if (fault is not null)
        {
            throw CreateException("Injected actor invocation failure.", fault.Transient);
        }
    }

    private static Exception CreateException(string message, bool transient) =>
        transient ? new ActorInjectedTransientException(message) : new ActorInjectedPermanentException(message);

    private static TFault? DequeueFirstMatch<TFault>(Queue<TFault> faults, Func<TFault, bool> predicate)
        where TFault : class
    {
        var count = faults.Count;
        TFault? match = null;
        for (var index = 0; index < count; index++)
        {
            var fault = faults.Dequeue();
            if (match is null && predicate(fault))
            {
                match = fault;
                continue;
            }

            faults.Enqueue(fault);
        }

        return match;
    }

    private sealed record StateWriteFault(Type StateType, bool Transient, string? StateName);

    private sealed record MigrationFault(Type TargetStateType, bool Transient, string? StateName);

    private sealed record UpcastHopFault(Type FromStateType, Type ToStateType, bool Transient, string? StateName);

    private sealed record InvocationFault(bool Transient, string? ActorType, string? MethodName);
}
