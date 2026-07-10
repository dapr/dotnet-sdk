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

namespace Dapr.Actors.Next.StateMachine;

/// <summary>
/// Non-generic runtime view of an effect context used after event-type-specific dispatch.
/// </summary>
internal interface IRuntimeEffectContext
{
    /// <summary>
    /// Gets the reply value supplied by the effect, if any.
    /// </summary>
    object? ReplyValue { get; }

    /// <summary>
    /// Gets a value indicating whether a reply was supplied.
    /// </summary>
    bool HasReply { get; }
}

/// <summary>
/// Runtime effect context passed to guards, entry/exit actions, and transition effects.
/// </summary>
internal sealed class EffectContext<TState, TData, TEvent>(
    TState state,
    Func<TData> getData,
    Action<TData> setData,
    TEvent actorEvent,
    IActorTimerEffects timers,
    Queue<object> raisedEvents) : IEffectContext<TState, TData, TEvent>, IRuntimeEffectContext
    where TState : struct, Enum
{
    /// <inheritdoc />
    public TState State { get; } = state;

    /// <inheritdoc />
    public TData Data => getData();

    /// <inheritdoc />
    public TEvent Event { get; } = actorEvent;

    /// <inheritdoc />
    public IActorTimerEffects Timers { get; } = timers;

    /// <inheritdoc />
    public object? ReplyValue { get; private set; }

    /// <inheritdoc />
    public bool HasReply { get; private set; }

    /// <inheritdoc />
    public void Update(Func<TData, TData> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        setData(update(getData()));
    }

    /// <inheritdoc />
    public void Raise<TInternalEvent>(TInternalEvent evt) => raisedEvents.Enqueue(evt!);

    /// <inheritdoc />
    public void Reply<TReply>(TReply value)
    {
        ReplyValue = value;
        HasReply = true;
    }
}
