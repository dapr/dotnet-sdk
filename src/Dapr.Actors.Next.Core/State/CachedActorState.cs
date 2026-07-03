using Dapr.Actors.Next.Abstractions.State;

namespace Dapr.Actors.Next.Core.State;

/// <summary>
/// Mutable cached actor state value.
/// </summary>
public sealed class CachedActorState<T> : IActorState<T>
{
    private T value;

    internal CachedActorState(string name, int schemaVersion, T value, Action onChanged)
    {
        Name = name;
        SchemaVersion = schemaVersion;
        this.value = value;
        this.onChanged = onChanged;
    }

    private readonly Action onChanged;

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public int SchemaVersion { get; }

    /// <inheritdoc />
    public T Value
    {
        get => value;
        set
        {
            this.value = value;
            onChanged();
        }
    }
}
