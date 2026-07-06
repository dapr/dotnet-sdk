namespace Dapr.Actors.Next.Abstractions.State;

/// <summary>
/// Represents typed actor state.
/// </summary>
public interface IActorState<T>
{
    /// <summary>
    /// Gets the state name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets or sets the typed state value.
    /// </summary>
    T Value { get; set; }
}
