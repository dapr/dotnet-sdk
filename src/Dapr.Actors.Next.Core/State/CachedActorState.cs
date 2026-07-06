using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Abstractions.State.Versioning;

namespace Dapr.Actors.Next.Core.State;

/// <summary>
/// Mutable cached actor state value.
/// </summary>
public sealed class CachedActorState<T> : IActorState<T>
{
    private T value;

    internal CachedActorState(string name, T value, Action onChanged, ActorStateMigrationNode? migrationNode, bool storePlain)
    {
        Name = name;
        this.value = value;
        this.onChanged = onChanged;
        MigrationNode = migrationNode;
        StorePlain = storePlain;
    }

    private readonly Action onChanged;

    /// <inheritdoc />
    public string Name { get; }

    internal ActorStateMigrationNode? MigrationNode { get; private set; }

    internal bool StorePlain { get; private set; }

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

    internal void StoreAsPlain()
    {
        MigrationNode = null;
        StorePlain = true;
        onChanged();
    }
}
