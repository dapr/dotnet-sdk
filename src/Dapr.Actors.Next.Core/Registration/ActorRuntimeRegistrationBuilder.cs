using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Dispatching;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Core.Activation;

namespace Dapr.Actors.Next.Core.Registration;

/// <summary>
/// Collects generated actor registrations before the service provider is built.
/// </summary>
public sealed class ActorRuntimeRegistrationBuilder
{
    private readonly List<ActorRuntimeRegistration> registrations = [];

    /// <summary>
    /// Adds an actor registration.
    /// </summary>
    public ActorRuntimeRegistrationBuilder Add(
        string actorType,
        Type interfaceType,
        Type implementationType,
        Func<IServiceProvider, ActorId, IActor> factory,
        IActorDispatcher dispatcher,
        ActorLifecycle? lifecycle = null,
        DaprActorsOptions? options = null)
    {
        registrations.Add(new ActorRuntimeRegistration(actorType, interfaceType, implementationType, factory, dispatcher, lifecycle, options));
        return this;
    }

    /// <summary>
    /// Adds an actor registration whose dispatcher is resolved from the final service provider.
    /// </summary>
    public ActorRuntimeRegistrationBuilder Add(
        string actorType,
        Type interfaceType,
        Type implementationType,
        Func<IServiceProvider, ActorId, IActor> factory,
        Func<IServiceProvider, IActorDispatcher> dispatcherFactory,
        ActorLifecycle? lifecycle = null,
        DaprActorsOptions? options = null)
    {
        registrations.Add(new ActorRuntimeRegistration(actorType, interfaceType, implementationType, factory, dispatcherFactory, lifecycle, options));
        return this;
    }

    internal ActorRuntimeRegistry Build(IServiceProvider services) => new(registrations, services);
}
