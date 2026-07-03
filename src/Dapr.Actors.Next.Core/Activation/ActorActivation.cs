using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Core.State;

namespace Dapr.Actors.Next.Core.Activation;

/// <summary>
/// Represents a live actor activation and its activation-owned services.
/// </summary>
public sealed class ActorActivation : IAsyncDisposable
{
    private readonly IAsyncDisposable activationServices;
    private readonly ActorLifecycle lifecycle;
    private bool disposed;

    internal ActorActivation(
        string actorType,
        ActorId actorId,
        IActor instance,
        IActorStateAccessor state,
        ActorStateUnitOfWork stateUnitOfWork,
        IAsyncDisposable activationServices,
        ActorLifecycle lifecycle)
    {
        ActorType = actorType;
        ActorId = actorId;
        Instance = instance;
        State = state;
        StateUnitOfWork = stateUnitOfWork;
        this.activationServices = activationServices;
        this.lifecycle = lifecycle;
    }

    /// <summary>
    /// Gets the registered actor type name.
    /// </summary>
    public string ActorType { get; }

    /// <summary>
    /// Gets the actor id.
    /// </summary>
    public ActorId ActorId { get; }

    /// <summary>
    /// Gets the activated actor instance.
    /// </summary>
    public IActor Instance { get; }

    /// <summary>
    /// Gets the state accessor bound to this activation.
    /// </summary>
    public IActorStateAccessor State { get; }

    internal ActorStateUnitOfWork StateUnitOfWork { get; }

    internal ValueTask OnActivateAsync(CancellationToken cancellationToken) =>
        lifecycle.OnActivateAsync(Instance, cancellationToken);

    internal ValueTask OnDeactivateAsync(CancellationToken cancellationToken) =>
        lifecycle.OnDeactivateAsync(Instance, cancellationToken);

    internal ValueTask OnPreActorMethodAsync(ActorMethodContext context, CancellationToken cancellationToken) =>
        lifecycle.OnPreActorMethodAsync(Instance, context, cancellationToken);

    internal ValueTask OnPostActorMethodAsync(ActorMethodContext context, Exception? exception, CancellationToken cancellationToken) =>
        lifecycle.OnPostActorMethodAsync(Instance, context, exception, cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (Instance is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (Instance is IDisposable disposable)
        {
            disposable.Dispose();
        }

        await activationServices.DisposeAsync().ConfigureAwait(false);
    }
}
