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
/// Configures a state machine over discrete and extended actor state.
/// </summary>
public interface IStateMachine<TState, TData>
    where TState : struct, Enum
{
    /// <summary>
    /// Sets the initial state for a new actor instance.
    /// </summary>
    IStateMachine<TState, TData> InitialState(TState state);

    /// <summary>
    /// Configures a state.
    /// </summary>
    IStateConfiguration<TState, TData> In(TState state);

    /// <summary>
    /// Configures the global unhandled event fallback.
    /// </summary>
    IStateMachine<TState, TData> OnUnhandled(Func<IEffectContext<TState, TData, object>, ValueTask> handler);
}
