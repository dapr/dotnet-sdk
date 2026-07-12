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
/// Configures one state in a state machine.
/// </summary>
public interface IStateConfiguration<TState, TData>
    where TState : struct, Enum
{
    /// <summary>
    /// Declares a parent state.
    /// </summary>
    IStateConfiguration<TState, TData> SubstateOf(TState parent);

    /// <summary>
    /// Configures entry behavior.
    /// </summary>
    IStateConfiguration<TState, TData> OnEntry(Func<IEffectContext<TState, TData, object>, ValueTask> action);

    /// <summary>
    /// Configures exit behavior.
    /// </summary>
    IStateConfiguration<TState, TData> OnExit(Func<IEffectContext<TState, TData, object>, ValueTask> action);

    /// <summary>
    /// Configures an event handler.
    /// </summary>
    IEventConfiguration<TState, TData, TEvent> On<TEvent>();

    /// <summary>
    /// Ignores an event in this state.
    /// </summary>
    IStateConfiguration<TState, TData> Ignore<TEvent>();

    /// <summary>
    /// Durably defers an event in this state.
    /// </summary>
    IStateConfiguration<TState, TData> Defer<TEvent>();

    /// <summary>
    /// Configures a state timeout.
    /// </summary>
    IStateConfiguration<TState, TData> After(TimeSpan timeout);
}
