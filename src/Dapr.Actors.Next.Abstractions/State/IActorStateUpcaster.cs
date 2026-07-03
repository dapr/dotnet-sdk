namespace Dapr.Actors.Next.Abstractions.State;

/// <summary>
/// Upcasts one actor state schema version to the next typed schema version.
/// </summary>
public interface IActorStateUpcaster<in TFromType, TToType>
{
    /// <summary>
    /// Converts state from <typeparamref name="TFromType"/> to <typeparamref name="TToType"/>.
    /// </summary>
    ValueTask<TToType> UpcastAsync(TFromType state, CancellationToken cancellationToken = default);
}
