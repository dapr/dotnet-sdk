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
using Dapr.Actors.Next.Abstractions;

namespace Dapr.Actors.Next.Interpreted;

/// <summary>
/// In-memory interpreted machine-definition store for tests and single-process hosts.
/// </summary>
public sealed class InMemoryInterpretedMachineStore : IInterpretedMachineStore
{
    private readonly ConcurrentDictionary<Key, InterpretedMachineDefinition> definitions = [];

    /// <inheritdoc />
    public ValueTask<InterpretedMachineDefinition?> GetAsync(string actorType, ActorId actorId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        definitions.TryGetValue(new Key(actorType, actorId.Value), out var definition);
        return ValueTask.FromResult(definition);
    }

    /// <inheritdoc />
    public ValueTask SetAsync(string actorType, ActorId actorId, InterpretedMachineDefinition definition, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        definitions[new Key(actorType, actorId.Value)] = definition;
        return ValueTask.CompletedTask;
    }

    private sealed record Key(string ActorType, string ActorId);
}
