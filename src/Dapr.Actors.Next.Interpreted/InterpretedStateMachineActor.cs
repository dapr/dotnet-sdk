using System.Text.Json;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Filters;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Core.Activation;

namespace Dapr.Actors.Next.Interpreted;

/// <summary>
/// Single compiled actor that executes state-machine definitions supplied as data.
/// </summary>
public sealed class InterpretedStateMachineActor(
    ActorActivationContext context,
    string actorType,
    IInterpretedMachineStore definitions,
    IInterpretedMachineVerifier verifier,
    ICapabilityRegistry capabilities) : Actor
{
    private const string StateName = "__interpreted";
    private InterpretedMachineDefinition? definition;
    private string? currentState;
    private DynamicStateBag? data;

    /// <inheritdoc />
    protected override ActorId Id => context.ActorId;

    /// <inheritdoc />
    protected override IActorStateAccessor State => context.State;

    /// <summary>
    /// Raises an event into the interpreted machine.
    /// </summary>
    public async Task<InterpretedRaiseResult> RaiseAsync(InterpretedEvent evt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evt);
        await EnsureActivatedAsync(cancellationToken).ConfigureAwait(false);

        var transition = definition!.Transitions.FirstOrDefault(item =>
            string.Equals(item.Source, currentState, StringComparison.Ordinal)
            && string.Equals(item.Event, evt.Name, StringComparison.Ordinal));
        if (transition is null)
        {
            throw new InvalidOperationException($"State '{currentState}' does not handle event '{evt.Name}'.");
        }

        var branch = await SelectBranchAsync(transition, evt, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No branch matched event '{evt.Name}' in state '{currentState}'.");

        if (branch.Target is not null)
        {
            await RunEffectsAsync(StateDefinition(currentState!).ExitEffects, evt, cancellationToken).ConfigureAwait(false);
        }

        await RunEffectsAsync(branch.Effects, evt, cancellationToken).ConfigureAwait(false);

        if (branch.Target is not null)
        {
            currentState = branch.Target;
            await RunEffectsAsync(StateDefinition(currentState).EntryEffects, evt, cancellationToken).ConfigureAwait(false);
        }

        await PersistAsync(cancellationToken).ConfigureAwait(false);
        return new InterpretedRaiseResult(currentState!, branch.Reply?.Clone(), data!);
    }

    /// <summary>
    /// Purges the actor's persisted state so a later activation re-initializes from its deployed
    /// definition. Deliberately does not require a deployed definition, so it is a safe no-op on an
    /// actor that has never been onboarded.
    /// </summary>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await State.RemoveAsync(StateName, cancellationToken).ConfigureAwait(false);
        await State.RemoveAsync("__currentState", cancellationToken).ConfigureAwait(false);
        await State.RemoveAsync("__data", cancellationToken).ConfigureAwait(false);
        definition = null;
        currentState = null;
        data = null;
    }

    /// <inheritdoc />
    protected override ValueTask OnActivateAsync(CancellationToken cancellationToken = default) =>
        // Activation is lazy: the definition is loaded and verified on the first raised event (see
        // EnsureActivatedAsync in RaiseAsync). This keeps a definition-less actor activatable so it can
        // be reset/purged before it is ever onboarded.
        base.OnActivateAsync(cancellationToken);

    private async ValueTask EnsureActivatedAsync(CancellationToken cancellationToken)
    {
        if (definition is not null)
        {
            return;
        }

        definition = await definitions.GetAsync(actorType, Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No interpreted machine definition is deployed for '{actorType}/{Id}'.");
        verifier.Verify(definition).ThrowIfInvalid();

        var persisted = await State.TryGetAsync<InterpretedActorState>(StateName, cancellationToken).ConfigureAwait(false);
        if (persisted is null)
        {
            currentState = definition.InitialState;
            data = new DynamicStateBag(definition.InitialData);
        }
        else
        {
            currentState = persisted.Value.CurrentState;
            data = new DynamicStateBag(persisted.Value.Data);
        }
    }

    private async ValueTask<InterpretedBranchDefinition?> SelectBranchAsync(
        InterpretedTransitionDefinition transition,
        InterpretedEvent evt,
        CancellationToken cancellationToken)
    {
        foreach (var branch in transition.Branches)
        {
            if (branch.Otherwise)
            {
                return branch;
            }

            var passed = true;
            foreach (var guardName in branch.Guards)
            {
                if (!capabilities.TryGetGuard(guardName, out var guard))
                {
                    throw new InvalidOperationException($"Guard '{guardName}' is not registered.");
                }

                if (!await guard.EvaluateAsync(CreateCapabilityContext(guardName, evt), cancellationToken).ConfigureAwait(false))
                {
                    passed = false;
                    break;
                }
            }

            if (passed)
            {
                return branch;
            }
        }

        return null;
    }

    private async ValueTask RunEffectsAsync(IReadOnlyList<string> effects, InterpretedEvent evt, CancellationToken cancellationToken)
    {
        foreach (var effectName in effects)
        {
            if (!capabilities.TryGetEffect(effectName, out var effect))
            {
                throw new InvalidOperationException($"Effect '{effectName}' is not registered.");
            }

            await effect.ExecuteAsync(CreateCapabilityContext(effectName, evt), cancellationToken).ConfigureAwait(false);
        }
    }

    private ActorCapabilityContext CreateCapabilityContext(string capabilityName, InterpretedEvent evt) =>
        new(
            actorType,
            Id,
            capabilityName,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["state"] = data!,
                ["event"] = evt.Name,
                ["payload"] = evt.Payload,
                ["documentVersion"] = definition!.DocumentVersion,
            },
            new ActorRequestContext(null, null, new Dictionary<string, string>(StringComparer.Ordinal)));

    private InterpretedStateDefinition StateDefinition(string state) =>
        definition!.States.First(item => string.Equals(item.Name, state, StringComparison.Ordinal));

    private async ValueTask PersistAsync(CancellationToken cancellationToken)
    {
        var snapshot = new InterpretedActorState(definition!.DocumentVersion, currentState!, data!.ToDictionary());
        await State.SetAsync(StateName, snapshot, cancellationToken).ConfigureAwait(false);
        await State.SetAsync("__currentState", currentState!, cancellationToken).ConfigureAwait(false);
        await State.SetAsync("__data", data, cancellationToken).ConfigureAwait(false);
    }
}
