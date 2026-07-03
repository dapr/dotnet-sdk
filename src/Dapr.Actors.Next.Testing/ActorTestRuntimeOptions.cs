namespace Dapr.Actors.Next.Testing;

/// <summary>
/// Configures an <see cref="ActorTestRuntime"/>.
/// </summary>
public sealed class ActorTestRuntimeOptions
{
    /// <summary>
    /// Gets or sets the scheduler used by the runtime.
    /// </summary>
    public ControlledActorScheduler? Scheduler { get; set; }
}
