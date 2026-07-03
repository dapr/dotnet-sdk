using Dapr.Actors.Next.Abstractions.State;

namespace Dapr.Actors.Next.Abstractions;

/// <summary>
/// Base class for actor implementations.
/// </summary>
public abstract class Actor : IActor
{
    /// <summary>
    /// Gets the current actor id.
    /// </summary>
    protected abstract ActorId Id { get; }

    /// <summary>
    /// Gets the actor state accessor for the current activation.
    /// </summary>
    protected abstract IActorStateAccessor State { get; }

    /// <summary>
    /// Runs when the actor activation starts.
    /// </summary>
    protected virtual ValueTask OnActivateAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    /// <summary>
    /// Runs when the actor activation ends.
    /// </summary>
    protected virtual ValueTask OnDeactivateAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    /// <summary>
    /// Runs before an actor method turn is invoked.
    /// </summary>
    protected virtual ValueTask OnPreActorMethodAsync(ActorMethodContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    /// <summary>
    /// Runs after an actor method turn is invoked.
    /// </summary>
    protected virtual ValueTask OnPostActorMethodAsync(ActorMethodContext context, Exception? exception, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    /// <summary>
    /// Invokes the protected activation lifecycle hook for generated runtime delegates.
    /// </summary>
    public ValueTask InvokeOnActivateAsync(CancellationToken cancellationToken = default) => OnActivateAsync(cancellationToken);

    /// <summary>
    /// Invokes the protected deactivation lifecycle hook for generated runtime delegates.
    /// </summary>
    public ValueTask InvokeOnDeactivateAsync(CancellationToken cancellationToken = default) => OnDeactivateAsync(cancellationToken);

    /// <summary>
    /// Invokes the protected pre-method lifecycle hook for generated runtime delegates.
    /// </summary>
    public ValueTask InvokeOnPreActorMethodAsync(ActorMethodContext context, CancellationToken cancellationToken = default) =>
        OnPreActorMethodAsync(context, cancellationToken);

    /// <summary>
    /// Invokes the protected post-method lifecycle hook for generated runtime delegates.
    /// </summary>
    public ValueTask InvokeOnPostActorMethodAsync(ActorMethodContext context, Exception? exception, CancellationToken cancellationToken = default) =>
        OnPostActorMethodAsync(context, exception, cancellationToken);
}
