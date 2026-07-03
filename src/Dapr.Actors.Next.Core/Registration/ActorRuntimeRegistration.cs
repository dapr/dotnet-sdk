using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Dispatching;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Core.Activation;

namespace Dapr.Actors.Next.Core.Registration;

/// <summary>
/// Describes the generated runtime artifacts for one actor type.
/// </summary>
public sealed class ActorRuntimeRegistration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActorRuntimeRegistration"/> class.
    /// </summary>
    public ActorRuntimeRegistration(
        string actorType,
        Type interfaceType,
        Type implementationType,
        Func<IServiceProvider, ActorId, IActor> factory,
        IActorDispatcher dispatcher,
        ActorLifecycle? lifecycle = null,
        DaprActorsOptions? options = null)
        : this(actorType, interfaceType, implementationType, factory, _ => dispatcher ?? throw new ArgumentNullException(nameof(dispatcher)), lifecycle, options)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ActorRuntimeRegistration"/> class.
    /// </summary>
    public ActorRuntimeRegistration(
        string actorType,
        Type interfaceType,
        Type implementationType,
        Func<IServiceProvider, ActorId, IActor> factory,
        Func<IServiceProvider, IActorDispatcher> dispatcherFactory,
        ActorLifecycle? lifecycle = null,
        DaprActorsOptions? options = null)
    {
        ActorType = string.IsNullOrWhiteSpace(actorType) ? throw new ArgumentException("Actor type is required.", nameof(actorType)) : actorType;
        InterfaceType = interfaceType ?? throw new ArgumentNullException(nameof(interfaceType));
        ImplementationType = implementationType ?? throw new ArgumentNullException(nameof(implementationType));
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        DispatcherFactory = dispatcherFactory ?? throw new ArgumentNullException(nameof(dispatcherFactory));
        Lifecycle = lifecycle ?? ActorLifecycle.Empty;
        Options = options;
    }

    /// <summary>
    /// Gets the runtime actor type name.
    /// </summary>
    public string ActorType { get; }

    /// <summary>
    /// Gets the public actor interface type.
    /// </summary>
    public Type InterfaceType { get; }

    /// <summary>
    /// Gets the implementation type.
    /// </summary>
    public Type ImplementationType { get; }

    /// <summary>
    /// Gets the generated activation factory.
    /// </summary>
    public Func<IServiceProvider, ActorId, IActor> Factory { get; }

    /// <summary>
    /// Gets the generated method dispatcher.
    /// </summary>
    public Func<IServiceProvider, IActorDispatcher> DispatcherFactory { get; }

    /// <summary>
    /// Gets the generated lifecycle delegate set.
    /// </summary>
    public ActorLifecycle Lifecycle { get; }

    /// <summary>
    /// Gets the actor runtime options used by this actor type's stream.
    /// </summary>
    public DaprActorsOptions? Options { get; }

    internal IActorDispatcher Dispatcher { get; private set; } = null!;

    internal void ResolveDispatcher(IServiceProvider services) => Dispatcher = DispatcherFactory(services);
}
