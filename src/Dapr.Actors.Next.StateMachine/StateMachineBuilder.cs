namespace Dapr.Actors.Next.StateMachine;

/// <summary>
/// Builds a compiled state-machine transition table.
/// </summary>
public sealed class StateMachineBuilder<TState, TData> : IStateMachine<TState, TData>
    where TState : struct, Enum
{
    private readonly Dictionary<TState, RuntimeState<TState, TData>> states = [];
    private object? unhandled;
    private BehaviorRef? unhandledRef;
    private TState? initialState;

    /// <inheritdoc />
    public IStateMachine<TState, TData> InitialState(TState state)
    {
        initialState = state;
        GetOrCreateState(state);
        return this;
    }

    /// <inheritdoc />
    public IStateConfiguration<TState, TData> In(TState state) => new StateConfiguration(this, GetOrCreateState(state));

    /// <inheritdoc />
    public IStateMachine<TState, TData> OnUnhandled(Action<IEffectContext<TState, TData, object>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        unhandled = handler;
        unhandledRef = BehaviorRef.ForDelegate(handler);
        return this;
    }

    /// <inheritdoc />
    public IStateMachine<TState, TData> OnUnhandled(Func<IEffectContext<TState, TData, object>, ValueTask> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        unhandled = handler;
        unhandledRef = BehaviorRef.ForDelegate(handler);
        return this;
    }

    /// <inheritdoc />
    public IStateMachine<TState, TData> OnUnhandled(string handlerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerName);
        unhandled = handlerName;
        unhandledRef = BehaviorRef.ForName(handlerName);
        return this;
    }

    /// <summary>
    /// Builds the transition table.
    /// </summary>
    public TransitionTable<TState, TData> Build()
    {
        var nodes = states.Values
            .OrderBy(state => state.State.ToString(), StringComparer.Ordinal)
            .Select(state => new StateNode<TState>(
                state.State,
                state.Parent,
                state.Timeout,
                state.EntryActions.Select(action => ToBehaviorRef(action.Action)).ToArray(),
                state.ExitActions.Select(action => ToBehaviorRef(action.Action)).ToArray()))
            .ToArray();

        var transitions = states.Values
            .OrderBy(state => state.State.ToString(), StringComparer.Ordinal)
            .SelectMany(state => BuildTransitionNodes(state))
            .ToArray();

        return new TransitionTable<TState, TData>
        {
            InitialState = initialState,
            States = nodes,
            Transitions = transitions,
            Unhandled = unhandledRef,
            RuntimeStates = states,
        };
    }

    /// <summary>
    /// Gets the executable unhandled-event behavior configured for this machine.
    /// </summary>
    internal object? Unhandled => unhandled;

    /// <summary>
    /// Gets an existing runtime state node or creates a new one.
    /// </summary>
    internal RuntimeState<TState, TData> GetOrCreateState(TState state)
    {
        if (!states.TryGetValue(state, out var configuration))
        {
            configuration = new RuntimeState<TState, TData> { State = state };
            states[state] = configuration;
        }

        return configuration;
    }

    /// <summary>
    /// Converts a compiled delegate or named capability into a serializable behavior reference.
    /// </summary>
    private static BehaviorRef ToBehaviorRef(object action) =>
        action is Delegate value ? BehaviorRef.ForDelegate(value) : BehaviorRef.ForName((string)action);

    /// <summary>
    /// Builds the serializable transition nodes for one executable state node.
    /// </summary>
    private static IEnumerable<TransitionNode<TState>> BuildTransitionNodes(RuntimeState<TState, TData> state)
    {
        foreach (var ignored in state.IgnoredEvents)
        {
            yield return new TransitionNode<TState>(state.State, ignored.AssemblyQualifiedName!, true, false, []);
        }

        foreach (var deferred in state.DeferredEvents)
        {
            yield return new TransitionNode<TState>(state.State, deferred.AssemblyQualifiedName!, false, true, []);
        }

        foreach (var handler in state.Handlers.Values)
        {
            var eventName = handler.EventType.AssemblyQualifiedName!;
            var branches = handler.Branches.Select(branch => new GuardBranchNode<TState>(
                branch.GuardName ?? (branch.Guard is null ? null : branch.Guard.Method.Name),
                branch.Otherwise,
                branch.Target,
                branch.IsInternal,
                branch.Effects.Select(ToBehaviorRef).ToArray(),
                branch.ReplyValue)).ToArray();

            yield return new TransitionNode<TState>(state.State, eventName, false, false, branches);
        }
    }

    /// <summary>
    /// Fluent state configuration implementation backed by one runtime state node.
    /// </summary>
    private sealed class StateConfiguration(StateMachineBuilder<TState, TData> builder, RuntimeState<TState, TData> state) : IStateConfiguration<TState, TData>
    {
        /// <inheritdoc />
        public IStateConfiguration<TState, TData> SubstateOf(TState parent)
        {
            state.Parent = parent;
            builder.GetOrCreateState(parent);
            return this;
        }

        /// <inheritdoc />
        public IStateConfiguration<TState, TData> OnEntry(Action<IEffectContext<TState, TData, object>> action)
        {
            state.EntryActions.Add(new RuntimeAction<TState, TData, object>(action));
            return this;
        }

        /// <inheritdoc />
        public IStateConfiguration<TState, TData> OnEntry(Func<IEffectContext<TState, TData, object>, ValueTask> action)
        {
            state.EntryActions.Add(new RuntimeAction<TState, TData, object>(action));
            return this;
        }

        /// <inheritdoc />
        public IStateConfiguration<TState, TData> OnEntry(string actionName)
        {
            state.EntryActions.Add(new RuntimeAction<TState, TData, object>(actionName));
            return this;
        }

        /// <inheritdoc />
        public IStateConfiguration<TState, TData> OnExit(Action<IEffectContext<TState, TData, object>> action)
        {
            state.ExitActions.Add(new RuntimeAction<TState, TData, object>(action));
            return this;
        }

        /// <inheritdoc />
        public IStateConfiguration<TState, TData> OnExit(Func<IEffectContext<TState, TData, object>, ValueTask> action)
        {
            state.ExitActions.Add(new RuntimeAction<TState, TData, object>(action));
            return this;
        }

        /// <inheritdoc />
        public IStateConfiguration<TState, TData> OnExit(string actionName)
        {
            state.ExitActions.Add(new RuntimeAction<TState, TData, object>(actionName));
            return this;
        }

        /// <inheritdoc />
        public IEventConfiguration<TState, TData, TEvent> On<TEvent>()
        {
            if (!state.Handlers.TryGetValue(typeof(TEvent), out var handler))
            {
                handler = new RuntimeHandler<TState, TData> { EventType = typeof(TEvent) };
                state.Handlers[typeof(TEvent)] = handler;
            }

            return new EventConfiguration<TEvent>(handler);
        }

        /// <inheritdoc />
        public IStateConfiguration<TState, TData> Ignore<TEvent>()
        {
            state.IgnoredEvents.Add(typeof(TEvent));
            return this;
        }

        /// <inheritdoc />
        public IStateConfiguration<TState, TData> Defer<TEvent>()
        {
            state.DeferredEvents.Add(typeof(TEvent));
            return this;
        }

        /// <inheritdoc />
        public IStateConfiguration<TState, TData> After(TimeSpan timeout)
        {
            if (timeout < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout cannot be negative.");
            }

            state.Timeout = timeout;
            return this;
        }
    }

    /// <summary>
    /// Fluent event and branch configuration implementation backed by one runtime event handler.
    /// </summary>
    private sealed class EventConfiguration<TEvent>(RuntimeHandler<TState, TData> handler) :
        IEventConfiguration<TState, TData, TEvent>,
        IEventBranchConfiguration<TState, TData, TEvent>
    {
        private RuntimeBranch<TState, TData>? currentBranch;

        /// <inheritdoc />
        public IEventBranchConfiguration<TState, TData, TEvent> When(Func<TData, TEvent, bool> predicate)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            currentBranch = new RuntimeBranch<TState, TData> { Guard = predicate };
            handler.Branches.Add(currentBranch);
            return this;
        }

        /// <inheritdoc />
        public IEventBranchConfiguration<TState, TData, TEvent> When(string guardName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(guardName);
            currentBranch = new RuntimeBranch<TState, TData> { GuardName = guardName };
            handler.Branches.Add(currentBranch);
            return this;
        }

        /// <inheritdoc />
        public IEventBranchConfiguration<TState, TData, TEvent> Otherwise()
        {
            currentBranch = new RuntimeBranch<TState, TData> { Otherwise = true };
            handler.Branches.Add(currentBranch);
            return this;
        }

        /// <inheritdoc />
        public IEventBranchConfiguration<TState, TData, TEvent> Do(Action<IEffectContext<TState, TData, TEvent>> effect)
        {
            ArgumentNullException.ThrowIfNull(effect);
            CurrentBranch().Effects.Add(effect);
            return this;
        }

        /// <inheritdoc />
        public IEventBranchConfiguration<TState, TData, TEvent> Do(Func<IEffectContext<TState, TData, TEvent>, ValueTask> effect)
        {
            ArgumentNullException.ThrowIfNull(effect);
            CurrentBranch().Effects.Add(effect);
            return this;
        }

        /// <inheritdoc />
        public IEventBranchConfiguration<TState, TData, TEvent> Do(string effectName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(effectName);
            CurrentBranch().Effects.Add(effectName);
            return this;
        }

        /// <inheritdoc />
        public IEventBranchConfiguration<TState, TData, TEvent> GoTo(TState state)
        {
            var branch = CurrentBranch();
            branch.Target = state;
            branch.IsInternal = false;
            return this;
        }

        /// <inheritdoc />
        public IEventBranchConfiguration<TState, TData, TEvent> Reply<TReply>(TReply value)
        {
            CurrentBranch().ReplyValue = value;
            return this;
        }

        /// <inheritdoc />
        public IEventBranchConfiguration<TState, TData, TEvent> Raise<TInternalEvent>(TInternalEvent evt)
        {
            CurrentBranch().Effects.Add(new Action<IEffectContext<TState, TData, TEvent>>(ctx => ctx.Raise(evt!)));
            return this;
        }

        /// <summary>
        /// Gets the branch currently being configured, creating the implicit otherwise branch when needed.
        /// </summary>
        private RuntimeBranch<TState, TData> CurrentBranch()
        {
            if (currentBranch is null)
            {
                currentBranch = new RuntimeBranch<TState, TData> { Otherwise = true };
                handler.Branches.Add(currentBranch);
            }

            return currentBranch;
        }
    }
}
