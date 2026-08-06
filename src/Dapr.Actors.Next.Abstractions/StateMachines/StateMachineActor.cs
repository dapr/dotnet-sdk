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

namespace Dapr.Actors.Next.Abstractions.StateMachines;

/// <summary>
/// Base type for actor implementations authored as state machines.
/// </summary>
public abstract class StateMachineActor<TState, TData> : Actor
    where TState : struct, Enum
{
    /// <summary>
    /// Gets the current discrete state.
    /// </summary>
    protected abstract TState CurrentState { get; }

    /// <summary>
    /// Gets the current extended state payload.
    /// </summary>
    protected abstract TData Data { get; }

    /// <summary>
    /// Configures the state machine table.
    /// </summary>
    protected abstract void Configure(IStateMachine<TState, TData> stateMachine);

    /// <summary>
    /// Raises an event into the state machine and returns the reply value.
    /// </summary>
    protected abstract Task<TReply> RaiseAsync<TEvent, TReply>(TEvent evt, CancellationToken cancellationToken = default);
}
