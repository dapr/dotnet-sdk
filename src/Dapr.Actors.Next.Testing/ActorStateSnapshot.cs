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

namespace Dapr.Actors.Next.Testing;

/// <summary>
/// Provides typed access to an actor's persisted state during a deterministic test.
/// </summary>
public sealed class ActorStateSnapshot(ActorTestRuntime runtime, string actorType, string actorId)
{
    /// <summary>
    /// Reads a named state value.
    /// </summary>
    public T? Get<T>(string name = "state") => runtime.ReadState<T>(actorType, actorId, name);

    /// <summary>
    /// Reads the default current-state slot used by state-machine tests.
    /// </summary>
    public TState? CurrentState<TState>() => runtime.ReadState<TState>(actorType, actorId, "__currentState");

    /// <summary>
    /// Reads the default data slot used by state-machine tests.
    /// </summary>
    public TData? Data<TData>() => runtime.ReadState<TData>(actorType, actorId, "__data");
}
