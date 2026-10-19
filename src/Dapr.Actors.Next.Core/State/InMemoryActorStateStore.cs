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

using System.Collections.Concurrent;

namespace Dapr.Actors.Next.Core.State;

/// <summary>
/// In-memory actor state store used by tests and local hosts without a sidecar adapter.
/// </summary>
public sealed class InMemoryActorStateStore : IActorStateStore
{
    private readonly ConcurrentDictionary<StateKey, byte[]> values = new();
    private readonly object syncRoot = new();

    /// <inheritdoc />
    public ValueTask ApplyAsync(string actorType, string actorId, IReadOnlyList<ActorStateOperation> operations, CancellationToken cancellationToken = default)
    {
        var changes = operations.Select(operation => (Key: new StateKey(actorType, actorId, operation.Name), Value: operation.Value?.ToArray())).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        lock (syncRoot)
        {
            foreach (var change in changes)
            {
                if (change.Value is null)
                    values.TryRemove(change.Key, out _);
                else
                    values[change.Key] = change.Value;
            }
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>?> ReadAsync(string actorType, string actorId, string name, CancellationToken cancellationToken = default)
    {
        lock (syncRoot)
        {
            return values.TryGetValue(new StateKey(actorType, actorId, name), out var value)
                ? new ValueTask<ReadOnlyMemory<byte>?>(value.ToArray())
                : new ValueTask<ReadOnlyMemory<byte>?>((ReadOnlyMemory<byte>?)null);
        }
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(string actorType, string actorId, string name, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
    {
        return ApplyAsync(actorType, actorId, [new(name, value)], cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DeleteAsync(string actorType, string actorId, string name, CancellationToken cancellationToken = default)
    {
        return ApplyAsync(actorType, actorId, [new(name, null)], cancellationToken);
    }

    private readonly record struct StateKey(string ActorType, string ActorId, string Name);
}
