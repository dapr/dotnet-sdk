namespace Dapr.Actors.Next.StateMachine;

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
    /// Configures the global unhandled-event fallback.
    /// </summary>
    IStateMachine<TState, TData> OnUnhandled(Action<IEffectContext<TState, TData, object>> handler);

    /// <summary>
    /// Configures the global unhandled-event fallback.
    /// </summary>
    IStateMachine<TState, TData> OnUnhandled(Func<IEffectContext<TState, TData, object>, ValueTask> handler);

    /// <summary>
    /// Configures the global unhandled-event fallback as a named capability.
    /// </summary>
    IStateMachine<TState, TData> OnUnhandled(string handlerName);
}

/// <summary>
/// Configures one state in a state machine.
/// </summary>
public interface IStateConfiguration<TState, TData>
    where TState : struct, Enum
{
    /// <summary>
    /// Declares this state as a substate of the supplied parent.
    /// </summary>
    IStateConfiguration<TState, TData> SubstateOf(TState parent);

    /// <summary>
    /// Adds an entry action.
    /// </summary>
    IStateConfiguration<TState, TData> OnEntry(Action<IEffectContext<TState, TData, object>> action);

    /// <summary>
    /// Adds an entry action.
    /// </summary>
    IStateConfiguration<TState, TData> OnEntry(Func<IEffectContext<TState, TData, object>, ValueTask> action);

    /// <summary>
    /// Adds a named entry action.
    /// </summary>
    IStateConfiguration<TState, TData> OnEntry(string actionName);

    /// <summary>
    /// Adds an exit action.
    /// </summary>
    IStateConfiguration<TState, TData> OnExit(Action<IEffectContext<TState, TData, object>> action);

    /// <summary>
    /// Adds an exit action.
    /// </summary>
    IStateConfiguration<TState, TData> OnExit(Func<IEffectContext<TState, TData, object>, ValueTask> action);

    /// <summary>
    /// Adds a named exit action.
    /// </summary>
    IStateConfiguration<TState, TData> OnExit(string actionName);

    /// <summary>
    /// Configures handling for an event type in this state.
    /// </summary>
    IEventConfiguration<TState, TData, TEvent> On<TEvent>();

    /// <summary>
    /// Ignores an event type in this state.
    /// </summary>
    IStateConfiguration<TState, TData> Ignore<TEvent>();

    /// <summary>
    /// Durably defers an event type in this state.
    /// </summary>
    IStateConfiguration<TState, TData> Defer<TEvent>();

    /// <summary>
    /// Schedules a declarative timeout whenever this state is entered.
    /// </summary>
    IStateConfiguration<TState, TData> After(TimeSpan timeout);
}

/// <summary>
/// Configures an event handler and its guard chain.
/// </summary>
public interface IEventConfiguration<TState, TData, TEvent> : IEventBranchConfiguration<TState, TData, TEvent>
    where TState : struct, Enum
{
}

/// <summary>
/// Configures one branch in an event handler.
/// </summary>
public interface IEventBranchConfiguration<TState, TData, TEvent>
    where TState : struct, Enum
{
    /// <summary>
    /// Adds another guarded branch.
    /// </summary>
    IEventBranchConfiguration<TState, TData, TEvent> When(Func<TData, TEvent, bool> predicate);

    /// <summary>
    /// Adds another named guarded branch.
    /// </summary>
    IEventBranchConfiguration<TState, TData, TEvent> When(string guardName);

    /// <summary>
    /// Adds the guard-chain fallthrough branch.
    /// </summary>
    IEventBranchConfiguration<TState, TData, TEvent> Otherwise();

    /// <summary>
    /// Runs an effect.
    /// </summary>
    IEventBranchConfiguration<TState, TData, TEvent> Do(Action<IEffectContext<TState, TData, TEvent>> effect);

    /// <summary>
    /// Runs an effect.
    /// </summary>
    IEventBranchConfiguration<TState, TData, TEvent> Do(Func<IEffectContext<TState, TData, TEvent>, ValueTask> effect);

    /// <summary>
    /// Runs a named effect.
    /// </summary>
    IEventBranchConfiguration<TState, TData, TEvent> Do(string effectName);

    /// <summary>
    /// Performs an external transition to the supplied state.
    /// </summary>
    IEventBranchConfiguration<TState, TData, TEvent> GoTo(TState state);

    /// <summary>
    /// Supplies the reply returned by the command method.
    /// </summary>
    IEventBranchConfiguration<TState, TData, TEvent> Reply<TReply>(TReply value);

    /// <summary>
    /// Queues an internal event for run-to-completion processing.
    /// </summary>
    IEventBranchConfiguration<TState, TData, TEvent> Raise<TInternalEvent>(TInternalEvent evt);
}

/// <summary>
/// Effect context exposed to guards, entry/exit actions, and transition effects.
/// </summary>
public interface IEffectContext<TState, TData, TEvent>
    where TState : struct, Enum
{
    /// <summary>
    /// Gets the current state seen by the effect.
    /// </summary>
    TState State { get; }

    /// <summary>
    /// Gets the current extended state payload.
    /// </summary>
    TData Data { get; }

    /// <summary>
    /// Gets the event being handled.
    /// </summary>
    TEvent Event { get; }

    /// <summary>
    /// Gets the state-machine timer surface.
    /// </summary>
    IActorTimerEffects Timers { get; }

    /// <summary>
    /// Updates the extended state payload.
    /// </summary>
    void Update(Func<TData, TData> update);

    /// <summary>
    /// Queues an internal event for run-to-completion processing.
    /// </summary>
    void Raise<TInternalEvent>(TInternalEvent evt);

    /// <summary>
    /// Supplies the reply returned by the command method.
    /// </summary>
    void Reply<TReply>(TReply value);
}

/// <summary>
/// Timer operations available to state-machine effects.
/// </summary>
public interface IActorTimerEffects
{
    /// <summary>
    /// Schedules a named timer.
    /// </summary>
    void Schedule(string name, TimeSpan dueTime);

    /// <summary>
    /// Reschedules a named timer.
    /// </summary>
    void Reschedule(string name, TimeSpan dueTime);

    /// <summary>
    /// Cancels a named timer.
    /// </summary>
    void Cancel(string name);
}
