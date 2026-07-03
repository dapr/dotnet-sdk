namespace Dapr.Actors.Next.Abstractions.Options;

/// <summary>
/// Configures the Dapr Actors Next runtime.
/// </summary>
public sealed class DaprActorsOptions
{
    /// <summary>
    /// Gets the actor registrations requested explicitly by the app.
    /// </summary>
    public DaprActorRegistrationCollection Actors { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether discovered actors are automatically hosted.
    /// </summary>
    public bool EnableAutoActorRegistration { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether discovered actor state upcasters are automatically registered.
    /// </summary>
    public bool EnableAutoStateMigrationRegistration { get; set; } = true;

    /// <summary>
    /// Gets or sets the default contract version for generated registry entries.
    /// </summary>
    public int DefaultContractVersion { get; set; } = 1;

    /// <summary>
    /// Gets or sets the idle timeout for actor activations.
    /// </summary>
    public TimeSpan ActorIdleTimeout { get; set; } = TimeSpan.FromMinutes(60);

    /// <summary>
    /// Gets or sets the timeout used when draining ongoing actor calls.
    /// </summary>
    public TimeSpan DrainOngoingCallTimeout { get; private set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the timeout used when draining rebalanced actors.
    /// </summary>
    public TimeSpan DrainRebalancedActorsTimeout
    {
        get => DrainOngoingCallTimeout;
        set => DrainOngoingCallTimeout = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether rebalanced actors drain in-flight calls.
    /// </summary>
    public bool DrainRebalancedActors { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether reentrant actor calls are allowed.
    /// </summary>
    public bool EnableReentrancy { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the sidecar (gRPC) transport is used for actor state,
    /// timers, invocation, and the event stream. Enabled by default. When enabled, the underlying Dapr
    /// gRPC client is resolved lazily on first use, so it is not constructed while wiring up the runtime.
    /// Set to <see langword="false"/> to force the in-process fallbacks (in-memory state store, in-process
    /// invocation, and a disabled event stream) even when a Dapr gRPC client is registered.
    /// </summary>
    public bool EnableSidecarTransport { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum reentrant call depth when reentrancy is enabled.
    /// </summary>
    public int MaxReentrantDepth { get; set; } = 32;

    internal void CopyFrom(DaprActorsOptions source)
    {
        ArgumentNullException.ThrowIfNull(source);

        EnableAutoActorRegistration = source.EnableAutoActorRegistration;
        EnableAutoStateMigrationRegistration = source.EnableAutoStateMigrationRegistration;
        DefaultContractVersion = source.DefaultContractVersion;
        ActorIdleTimeout = source.ActorIdleTimeout;
        DrainOngoingCallTimeout = source.DrainOngoingCallTimeout;
        DrainRebalancedActors = source.DrainRebalancedActors;
        EnableReentrancy = source.EnableReentrancy;
        EnableSidecarTransport = source.EnableSidecarTransport;
        MaxReentrantDepth = source.MaxReentrantDepth;
        Actors.CopyFrom(source.Actors);
    }
}
