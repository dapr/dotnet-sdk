using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Filters;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dapr.Actors.Next.Interpreted;

/// <summary>
/// Registers Dapr Actors Next interpreted state-machine services.
/// </summary>
public static class DaprActorsInterpretedServiceCollectionExtensions
{
    /// <summary>
    /// Adds the interpreted machine runtime and registers the compiled interpreted actor type.
    /// </summary>
    public static IServiceCollection AddDaprInterpretedActors(
        this IServiceCollection services,
        string actorType = "InterpretedStateMachineActor")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);

        services.TryAddSingleton<IInterpretedMachineStore, InMemoryInterpretedMachineStore>();
        services.TryAddSingleton<IInterpretedMachineVerifier, InterpretedMachineVerifier>();
        services.TryAddSingleton<InterpretedMachineDeployer>();
        services.TryAddSingleton<InterpretedStateMachineDispatcher>();
        services.TryAddSingleton<ICapabilityRegistry, EmptyCapabilityRegistry>();
        services.AddDaprActorsCore(registrations =>
        {
            registrations.Add(
                actorType,
                typeof(IActor),
                typeof(InterpretedStateMachineActor),
                (sp, _) => new InterpretedStateMachineActor(
                    sp.GetRequiredService<ActorActivationContext>(),
                    actorType,
                    sp.GetRequiredService<IInterpretedMachineStore>(),
                    sp.GetRequiredService<IInterpretedMachineVerifier>(),
                    sp.GetRequiredService<ICapabilityRegistry>()),
                sp => sp.GetRequiredService<InterpretedStateMachineDispatcher>(),
                new ActorLifecycle(
                    static (actor, ct) => ((Actor)actor).InvokeOnActivateAsync(ct),
                    static (actor, ct) => ((Actor)actor).InvokeOnDeactivateAsync(ct),
                    static (actor, context, ct) => ((Actor)actor).InvokeOnPreActorMethodAsync(context, ct),
                    static (actor, context, exception, ct) => ((Actor)actor).InvokeOnPostActorMethodAsync(context, exception, ct)),
                new DaprActorsOptions { DisableStateMigration = true });
        });

        return services;
    }

    private sealed class EmptyCapabilityRegistry : ICapabilityRegistry
    {
        public bool TryGetEffect(string name, out IActorEffect effect)
        {
            effect = null!;
            return false;
        }

        public bool TryGetGuard(string name, out IActorGuard guard)
        {
            guard = null!;
            return false;
        }
    }
}
