namespace Dapr.Actors.Next.Abstractions.State;

/// <summary>
/// Configures actor state cache flush behavior.
/// </summary>
public sealed class DaprFlushStateOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DaprFlushStateOptions"/> class.
    /// </summary>
    public DaprFlushStateOptions()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprFlushStateOptions"/> class.
    /// </summary>
    public DaprFlushStateOptions(bool flushOnDirtyState)
    {
        FlushOnDirtyState = flushOnDirtyState;
    }

    /// <summary>
    /// Gets or sets a value indicating whether cached state may be unloaded when it has unpersisted changes.
    /// When <see langword="false"/>, cache flushing fails if any cached state entry is dirty.
    /// </summary>
    public bool FlushOnDirtyState { get; set; } = false;
}
