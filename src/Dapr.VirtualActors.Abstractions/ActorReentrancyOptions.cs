// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

namespace Dapr.VirtualActors;

/// <summary>
/// Configuration for actor reentrancy behavior.
/// </summary>
/// <remarks>
/// When reentrancy is enabled, an actor can call itself (or be called by another actor
/// in a call chain that loops back) without deadlocking. This is achieved through a
/// reentrancy ID passed in request headers that bypasses the turn-based concurrency lock.
/// </remarks>
public sealed class ActorReentrancyOptions
{
    /// <summary>
    /// Gets or sets whether reentrancy is enabled.
    /// </summary>
    /// <value><see langword="true"/> to enable reentrancy; otherwise <see langword="false"/>. Default is <see langword="false"/>.</value>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the maximum depth of reentrant calls allowed in a single chain.
    /// </summary>
    /// <value>
    /// The maximum stack depth. <see langword="null"/> means unlimited depth.
    /// Default is <see langword="null"/>.
    /// </value>
    public int? MaxStackDepth { get; set; }
}
