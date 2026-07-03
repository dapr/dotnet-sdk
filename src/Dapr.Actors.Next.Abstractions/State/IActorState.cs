namespace Dapr.Actors.Next.Abstractions.State;

/// <summary>
/// Represents typed actor state with the schema version carried by its persisted envelope.
/// </summary>
public interface IActorState<T>
{
    /// <summary>
    /// Gets the state name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the state schema version.
    /// </summary>
    int SchemaVersion { get; }

    /// <summary>
    /// Gets or sets the typed state value.
    /// </summary>
    T Value { get; set; }
}
