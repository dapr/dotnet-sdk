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

namespace Dapr.Actors.Next.Abstractions.Scheduling;

/// <summary>
/// Represents the runtime-owned mailbox for one actor activation.
/// </summary>
public interface IActorMailbox
{
    /// <summary>
    /// Gets the actor type name.
    /// </summary>
    string ActorType { get; }

    /// <summary>
    /// Gets the actor id.
    /// </summary>
    ActorId ActorId { get; }

    /// <summary>
    /// Enqueues a turn.
    /// </summary>
    ValueTask EnqueueAsync(ActorTurn turn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to dequeue the next turn.
    /// </summary>
    ValueTask<ActorTurn?> TryDequeueAsync(CancellationToken cancellationToken = default);
}
