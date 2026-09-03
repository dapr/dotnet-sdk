// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr.Actors.Next.Abstractions.Options;

using Dapr.Actors.Next.Abstractions.State.Versioning;

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
    /// Gets or sets a value indicating whether actor state migration is disabled and state is stored plainly.
    /// </summary>
    public bool DisableStateMigration { get; set; }

    /// <summary>
    /// Gets or sets the application-wide actor state versioning strategy type used by generators and analyzers.
    /// </summary>
    public IActorStateVersionStrategyType? ActorStateVersionStrategyType { get; set; }

    /// <summary>
    /// Gets or sets the default contract version for generated registry entries.
    /// </summary>
    public int DefaultContractVersion { get; set; } = 1;

    /// <summary>
    /// Gets or sets the idle timeout for actor activations.
    /// <see langword="null"/> leaves the value unset so the Dapr runtime default applies.
    /// </summary>
    public TimeSpan? ActorIdleTimeout { get; set; }

    /// <summary>
    /// Gets or sets the timeout used when draining ongoing actor calls.
    /// <see langword="null"/> leaves the value unset so the Dapr runtime default applies.
    /// </summary>
    public TimeSpan? DrainOngoingCallTimeout { get; set; }

    /// <summary>
    /// Gets or sets the timeout used when draining rebalanced actors.
    /// <see langword="null"/> leaves the value unset so the Dapr runtime default applies.
    /// </summary>
    public TimeSpan? DrainRebalancedActorsTimeout
    {
        get => DrainOngoingCallTimeout;
        set => DrainOngoingCallTimeout = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether rebalanced actors drain in-flight calls.
    /// <see langword="null"/> leaves the value unset so the Dapr runtime default applies.
    /// </summary>
    public bool? DrainRebalancedActors { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether reentrant actor calls are allowed.
    /// <see langword="null"/> leaves the value unset so the Dapr runtime default applies.
    /// </summary>
    public bool? EnableReentrancy { get; set; }

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
    /// <see langword="null"/> leaves the value unset so the Dapr runtime default applies.
    /// </summary>
    public int? MaxReentrantDepth { get; set; }

    internal void CopyFrom(DaprActorsOptions source)
    {
        ArgumentNullException.ThrowIfNull(source);

        EnableAutoActorRegistration = source.EnableAutoActorRegistration;
        EnableAutoStateMigrationRegistration = source.EnableAutoStateMigrationRegistration;
        DisableStateMigration = source.DisableStateMigration;
        ActorStateVersionStrategyType = source.ActorStateVersionStrategyType;
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
