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

/// <summary>
/// Configures runtime overrides for a single actor type. Every property is optional;
/// a <see langword="null"/> value inherits the corresponding app-wide value from
/// <see cref="DaprActorsOptions"/>. These settings are advertised to the Dapr sidecar
/// over the actor event stream, so they have no effect when
/// <see cref="DaprActorsOptions.EnableSidecarTransport"/> is <see langword="false"/>.
/// </summary>
public sealed class DaprActorTypeOptions
{
    /// <summary>
    /// Gets or sets the idle timeout after which the sidecar deactivates instances of this actor type.
    /// <see langword="null"/> inherits <see cref="DaprActorsOptions.ActorIdleTimeout"/>.
    /// </summary>
    public TimeSpan? IdleTimeout { get; set; }

    /// <summary>
    /// Gets or sets the timeout used when draining ongoing calls for this actor type.
    /// <see langword="null"/> inherits <see cref="DaprActorsOptions.DrainOngoingCallTimeout"/>.
    /// </summary>
    public TimeSpan? DrainOngoingCallTimeout { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether rebalanced actors of this type drain in-flight calls.
    /// <see langword="null"/> inherits <see cref="DaprActorsOptions.DrainRebalancedActors"/>.
    /// </summary>
    public bool? DrainRebalancedActors { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether reentrant calls are allowed for this actor type.
    /// <see langword="null"/> inherits <see cref="DaprActorsOptions.EnableReentrancy"/>.
    /// </summary>
    public bool? EnableReentrancy { get; set; }

    /// <summary>
    /// Gets or sets the maximum reentrant call depth for this actor type when reentrancy is enabled.
    /// <see langword="null"/> inherits <see cref="DaprActorsOptions.MaxReentrantDepth"/>.
    /// </summary>
    public int? MaxReentrantDepth { get; set; }
}
