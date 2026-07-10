namespace Dapr.Actors.Next.Abstractions.State;

/// <summary>
/// Configures actor state cache eviction behavior.
/// </summary>
public sealed class DaprEvictStateOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DaprEvictStateOptions"/> class.
    /// </summary>
    public DaprEvictStateOptions()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprEvictStateOptions"/> class.
    /// </summary>
    public DaprEvictStateOptions(bool evictOnDirtyState)
    {
        EvictOnDirtyState = evictOnDirtyState;
    }

    /// <summary>
    /// Gets or sets a value indicating whether cached state may be unloaded and discarded when it has unpersisted changes.
    /// When <see langword="false"/>, cache eviction fails if any cached state entry is dirty.
    /// </summary>
    public bool EvictOnDirtyState { get; set; } = false;
}
