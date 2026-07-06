using System.Runtime.CompilerServices;
using System.Text.Json;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Exceptions;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.Timers;

namespace Dapr.Actors.Next.StateMachine;

/// <summary>
/// Base type for actor implementations authored as state machines.
/// </summary>
public abstract class StateMachineActor<TState, TData> : Actor
    where TState : struct, Enum
{
    private readonly ActorActivationContext context;
    private readonly IActorTimerScheduler timerScheduler;
    private readonly string actorType;
    private readonly TData initialData;
    private readonly Queue<object> raisedEvents = [];
    private TransitionTable<TState, TData>? table;
    private object? unhandled;
    private List<DeferredEventEnvelope> deferredEvents = [];
    private bool restored;
    private bool processing;

    /// <summary>
    /// Initializes a new instance of the <see cref="StateMachineActor{TState, TData}"/> class.
    /// </summary>
    protected StateMachineActor(ActorActivationContext context, IActorTimerScheduler timerScheduler, string actorType, TData initialData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        this.timerScheduler = timerScheduler ?? throw new ArgumentNullException(nameof(timerScheduler));
        this.actorType = actorType;
        this.initialData = initialData;
        currentState = default;
        data = initialData;
    }

    private TState currentState;
    private TData data;

    /// <inheritdoc />
    protected override ActorId Id => context.ActorId;

    /// <inheritdoc />
    protected override IActorStateAccessor State => context.State;

    /// <summary>
    /// Gets the current discrete state.
    /// </summary>
    protected TState CurrentState => currentState;

    /// <summary>
    /// Gets the current extended state payload.
    /// </summary>
    protected TData Data => data;

    /// <summary>
    /// Configures the state machine table.
    /// </summary>
    protected abstract void Configure(IStateMachine<TState, TData> stateMachine);

    /// <summary>
    /// Raises an event into the state machine and returns the reply value.
    /// </summary>
    protected Task<TReply> Raise<TReply>(object evt, CancellationToken cancellationToken = default) =>
        RaiseAsync<TReply>(evt, cancellationToken);

    /// <summary>
    /// Raises an event into the state machine and returns the reply value.
    /// </summary>
    protected async Task<TReply> RaiseAsync<TReply>(object evt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evt);
        await EnsureRestoredAsync(cancellationToken).ConfigureAwait(false);

        if (processing)
        {
            raisedEvents.Enqueue(evt);
            return default!;
        }

        processing = true;
        object? reply = null;
        var hasReply = false;
        try
        {
            raisedEvents.Enqueue(evt);
            while (raisedEvents.Count > 0)
            {
                var next = raisedEvents.Dequeue();
                var result = await ProcessEventAsync(next, allowDefer: true, cancellationToken).ConfigureAwait(false);
                if (!hasReply && result.HasReply)
                {
                    reply = result.Reply;
                    hasReply = true;
                }
            }

            await PersistAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            processing = false;
        }

        return ConvertReply<TReply>(reply, hasReply);
    }

    /// <summary>
    /// Dispatches the reserved state-machine timer operation.
    /// </summary>
    public async Task DispatchStateMachineTimerAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        await EnsureRestoredAsync(cancellationToken).ConfigureAwait(false);
        var payload = JsonSerializer.Deserialize<StateMachineTimerPayload>(argumentsJson)
            ?? throw new InvalidOperationException("State-machine timer payload could not be deserialized.");

        if (string.Equals(payload.Name, StateMachineConstants.StateTimeoutTimerName, StringComparison.Ordinal))
        {
            if (!string.Equals(payload.State, currentState.ToString(), StringComparison.Ordinal))
            {
                return;
            }

            await RaiseAsync<object?>(new StateTimeout<TState>(currentState), cancellationToken).ConfigureAwait(false);
            return;
        }

        await RaiseAsync<object?>(new StateMachineTimerFired(payload.Name), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Exposes the configured transition table for deterministic analysis.
    /// </summary>
    public TransitionTable<TState, TData> GetTransitionTable()
    {
        EnsureConfigured();
        return table!;
    }

    /// <inheritdoc />
    protected override async ValueTask OnActivateAsync(CancellationToken cancellationToken = default)
    {
        await EnsureRestoredAsync(cancellationToken).ConfigureAwait(false);
        await base.OnActivateAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the transition table for a state-machine actor type without activating an actor instance.
    /// </summary>
    public static TransitionTable<TState, TData> BuildDefinitionFor(Type actorType)
    {
        var actor = (StateMachineActor<TState, TData>)RuntimeHelpers.GetUninitializedObject(actorType);
        var builder = new StateMachineBuilder<TState, TData>();
        actor.Configure(builder);
        return builder.Build();
    }

    private async ValueTask EnsureRestoredAsync(CancellationToken cancellationToken)
    {
        if (restored)
        {
            return;
        }

        EnsureConfigured();
        var envelope = await State.TryGetAsync<StateMachineEnvelope<TState, TData>>(StateMachineConstants.EnvelopeStateName, cancellationToken).ConfigureAwait(false);
        if (envelope is null)
        {
            currentState = table!.InitialState ?? throw new InvalidOperationException("State machine initial state was not configured.");
            data = initialData;
            deferredEvents = [];
        }
        else
        {
            currentState = envelope.Value.CurrentState;
            data = envelope.Value.Data;
            deferredEvents = envelope.Value.DeferredEvents.ToList();
        }

        restored = true;
    }

    private void EnsureConfigured()
    {
        if (table is not null)
        {
            return;
        }

        var builder = new StateMachineBuilder<TState, TData>();
        Configure(builder);
        table = builder.Build();
        unhandled = builder.Unhandled;
    }

    private async ValueTask<EventProcessResult> ProcessEventAsync(object evt, bool allowDefer, CancellationToken cancellationToken)
    {
        if (FindDisposition(currentState, evt.GetType()) == EventDisposition.Ignored)
        {
            return EventProcessResult.NoReply;
        }

        if (allowDefer && FindDisposition(currentState, evt.GetType()) == EventDisposition.Deferred)
        {
            DeferEvent(evt);
            return new EventProcessResult(true, DeferredEventAck.Accepted);
        }

        var handler = FindHandler(currentState, evt.GetType());
        if (handler is null)
        {
            return await HandleUnhandledAsync(evt, cancellationToken).ConfigureAwait(false);
        }

        var branch = SelectBranch(handler, evt);
        if (branch is null)
        {
            return EventProcessResult.NoReply;
        }

        var sourceState = currentState;
        var context = CreateRuntimeContext(sourceState, evt);
        if (!branch.IsInternal && branch.Target.HasValue)
        {
            await ExitToAsync(branch.Target.Value, evt, cancellationToken).ConfigureAwait(false);
        }

        foreach (var effect in branch.Effects)
        {
            await InvokeEffectAsync(effect, context, cancellationToken).ConfigureAwait(false);
        }

        if (branch.ReplyValue is not null)
        {
            SetReply(context, branch.ReplyValue);
        }

        if (!branch.IsInternal && branch.Target.HasValue)
        {
            currentState = branch.Target.Value;
            await EnterFromAsync(sourceState, currentState, evt, cancellationToken).ConfigureAwait(false);
        }

        await ReplayDeferredEventsAsync(cancellationToken).ConfigureAwait(false);

        return context.HasReply ? new EventProcessResult(true, context.ReplyValue) : EventProcessResult.NoReply;
    }

    private async ValueTask<EventProcessResult> HandleUnhandledAsync(object evt, CancellationToken cancellationToken)
    {
        if (unhandled is null)
        {
            throw new InvalidActorEventException(currentState, evt);
        }

        var context = CreateContext(currentState, evt);
        await InvokeEffectAsync(unhandled, context, cancellationToken).ConfigureAwait(false);
        return context.HasReply ? new EventProcessResult(true, context.ReplyValue) : EventProcessResult.NoReply;
    }

    private RuntimeBranch<TState, TData>? SelectBranch(RuntimeHandler<TState, TData> handler, object evt)
    {
        foreach (var branch in handler.Branches)
        {
            if (branch.Otherwise)
            {
                return branch;
            }

            if (branch.Guard is not null)
            {
                var result = (bool)branch.Guard.DynamicInvoke(data, evt)!;
                if (result)
                {
                    return branch;
                }

                continue;
            }

            if (branch.GuardName is not null)
            {
                throw new InvalidOperationException($"Named guard '{branch.GuardName}' cannot execute without a capability registry.");
            }

            return branch;
        }

        return null;
    }

    private RuntimeHandler<TState, TData>? FindHandler(TState state, Type eventType)
    {
        var current = state;
        while (table!.RuntimeStates.TryGetValue(current, out var configuration))
        {
            if (configuration.Handlers.TryGetValue(eventType, out var handler))
            {
                return handler;
            }

            if (!configuration.Parent.HasValue)
            {
                return null;
            }

            current = configuration.Parent.Value;
        }

        return null;
    }

    private EventDisposition FindDisposition(TState state, Type eventType)
    {
        var current = state;
        while (table!.RuntimeStates.TryGetValue(current, out var configuration))
        {
            if (configuration.IgnoredEvents.Contains(eventType))
            {
                return EventDisposition.Ignored;
            }

            if (configuration.DeferredEvents.Contains(eventType))
            {
                return EventDisposition.Deferred;
            }

            if (!configuration.Parent.HasValue)
            {
                return EventDisposition.None;
            }

            current = configuration.Parent.Value;
        }

        return EventDisposition.None;
    }

    private async ValueTask ExitToAsync(TState target, object evt, CancellationToken cancellationToken)
    {
        var lca = FindLeastCommonAncestor(currentState, target);
        foreach (var state in AncestorsFromChild(currentState).TakeWhile(state => !lca.HasValue || !EqualityComparer<TState>.Default.Equals(state, lca.Value)))
        {
            if (table!.RuntimeStates.TryGetValue(state, out var configuration))
            {
                if (configuration.Timeout.HasValue)
                {
                    await timerScheduler.CancelAsync(actorType, Id, StateMachineConstants.StateTimeoutTimerName, cancellationToken).ConfigureAwait(false);
                }

                foreach (var action in configuration.ExitActions)
                {
                    await InvokeEffectAsync(action.Action, CreateContext(state, evt), cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private async ValueTask EnterFromAsync(TState source, TState target, object evt, CancellationToken cancellationToken)
    {
        var lca = FindLeastCommonAncestor(source, target);
        foreach (var state in AncestorsFromChild(target).TakeWhile(state => !lca.HasValue || !EqualityComparer<TState>.Default.Equals(state, lca.Value)).Reverse())
        {
            if (table!.RuntimeStates.TryGetValue(state, out var configuration))
            {
                foreach (var action in configuration.EntryActions)
                {
                    await InvokeEffectAsync(action.Action, CreateContext(state, evt), cancellationToken).ConfigureAwait(false);
                }

                if (configuration.Timeout.HasValue)
                {
                    new ActorTimerEffects<TState>(timerScheduler, actorType, Id, () => currentState)
                        .Reschedule(StateMachineConstants.StateTimeoutTimerName, configuration.Timeout.Value);
                }
            }
        }
    }

    private TState? FindLeastCommonAncestor(TState source, TState target)
    {
        var sourceAncestors = AncestorsFromChild(source).ToHashSet();
        foreach (var ancestor in AncestorsFromChild(target))
        {
            if (sourceAncestors.Contains(ancestor))
            {
                return ancestor;
            }
        }

        return null;
    }

    private IEnumerable<TState> AncestorsFromChild(TState state)
    {
        var current = state;
        while (true)
        {
            yield return current;
            if (!table!.RuntimeStates.TryGetValue(current, out var configuration) || !configuration.Parent.HasValue)
            {
                yield break;
            }

            current = configuration.Parent.Value;
        }
    }

    private EffectContext<TState, TData, object> CreateContext(TState state, object evt) =>
        new(state, () => data, value => data = value, evt, new ActorTimerEffects<TState>(timerScheduler, actorType, Id, () => currentState), raisedEvents);

    private IRuntimeEffectContext CreateRuntimeContext(TState state, object evt)
    {
        var contextType = typeof(EffectContext<,,>).MakeGenericType(typeof(TState), typeof(TData), evt.GetType());
        return (IRuntimeEffectContext)Activator.CreateInstance(
            contextType,
            state,
            new Func<TData>(() => data),
            new Action<TData>(value => data = value),
            evt,
            new ActorTimerEffects<TState>(timerScheduler, actorType, Id, () => currentState),
            raisedEvents)!;
    }

    private static void SetReply(IRuntimeEffectContext context, object? value)
    {
        var method = context.GetType().GetMethod(nameof(IEffectContext<TState, TData, object>.Reply))!;
        method.MakeGenericMethod(value?.GetType() ?? typeof(object)).Invoke(context, [value]);
    }

    private static async ValueTask InvokeEffectAsync(object effect, object context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (effect)
        {
            case string name:
                throw new InvalidOperationException($"Named effect '{name}' cannot execute without a capability registry.");
            case Action<IEffectContext<TState, TData, object>> action:
                action((IEffectContext<TState, TData, object>)context);
                break;
            case Func<IEffectContext<TState, TData, object>, ValueTask> asyncAction:
                await asyncAction((IEffectContext<TState, TData, object>)context).ConfigureAwait(false);
                break;
            default:
                var result = effect.GetType().GetMethod("Invoke")!.Invoke(effect, [context]);
                if (result is ValueTask valueTask)
                {
                    await valueTask.ConfigureAwait(false);
                }

                break;
        }
    }

    private void DeferEvent(object evt)
    {
        var type = evt.GetType();
        deferredEvents.Add(new DeferredEventEnvelope(type.AssemblyQualifiedName!, JsonSerializer.Serialize(evt, type)));
    }

    private async ValueTask ReplayDeferredEventsAsync(CancellationToken cancellationToken)
    {
        for (var index = 0; index < deferredEvents.Count;)
        {
            var deferred = deferredEvents[index];
            var type = Type.GetType(deferred.TypeName, throwOnError: false);
            if (type is null)
            {
                index++;
                continue;
            }

            if (FindDisposition(currentState, type) == EventDisposition.Deferred)
            {
                index++;
                continue;
            }

            deferredEvents.RemoveAt(index);
            var evt = JsonSerializer.Deserialize(deferred.Json, type)
                ?? throw new InvalidOperationException($"Deferred event '{deferred.TypeName}' could not be deserialized.");
            await ProcessEventAsync(evt, allowDefer: false, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask PersistAsync(CancellationToken cancellationToken)
    {
        var envelope = new StateMachineEnvelope<TState, TData>(currentState, data, deferredEvents.ToArray());
        await State.SetAsync(StateMachineConstants.EnvelopeStateName, envelope, cancellationToken).ConfigureAwait(false);
        await State.SetAsync(StateMachineConstants.CurrentStateStateName, currentState, cancellationToken).ConfigureAwait(false);
        await State.SetAsync(StateMachineConstants.DataStateName, data, cancellationToken).ConfigureAwait(false);
    }

    private static TReply ConvertReply<TReply>(object? reply, bool hasReply)
    {
        if (!hasReply)
        {
            return default!;
        }

        if (reply is null)
        {
            return default!;
        }

        if (reply is TReply typed)
        {
            return typed;
        }

        throw new InvalidCastException($"State-machine reply of type '{reply.GetType()}' cannot be returned as '{typeof(TReply)}'.");
    }

    private readonly record struct EventProcessResult(bool HasReply, object? Reply)
    {
        public static EventProcessResult NoReply { get; } = new(false, null);
    }

    private enum EventDisposition
    {
        None,
        Ignored,
        Deferred,
    }
}
