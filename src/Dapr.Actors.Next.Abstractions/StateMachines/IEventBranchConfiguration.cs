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
/// Configures one event handler branch.
/// </summary>
public interface IEventBranchConfiguration<TState, TData, out TEvent>
    where TState : struct, Enum
{
    /// <summary>
    /// Runs an effect and remains in the current state unless a transition is also configured.
    /// </summary>
    IEventBranchConfiguration<TState, TData, TEvent> Do(Func<IEffectContext<TState, TData, TEvent>, ValueTask> effect);

    /// <summary>
    /// Transitions to another state.
    /// </summary>
    IEventBranchConfiguration<TState, TData, TEvent> GoTo(TState state);

    /// <summary>
    /// Replies to the invoking actor method.
    /// </summary>
    IEventBranchConfiguration<TState, TData, TEvent> Reply<TReply>(TReply value);
}
