namespace Microsoft.DurableTask;

/// <summary>
/// Defines properties and methods for task activity context objects.
/// </summary>
public abstract class ActivityContext
{
    /// <summary>
    /// Gets the name of the activity.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the unique ID of the current workflow instance.
    /// </summary>
    public abstract string InstanceId { get; }
}