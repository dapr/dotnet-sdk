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

using Dapr.Actors.Next.Abstractions.Scheduling;

namespace Dapr.Actors.Next.Core.Scheduling;

/// <summary>
/// Exposes deterministic mailbox execution to controlled schedulers.
/// </summary>
public interface IControlledActorMailbox : IActorMailbox
{
    /// <summary>
    /// Gets the number of turns waiting in the mailbox.
    /// </summary>
    int PendingCount { get; }

    /// <summary>
    /// Gets a value indicating whether the mailbox is currently executing a turn.
    /// </summary>
    bool IsExecuting { get; }

    /// <summary>
    /// Peeks at the next turn without dequeuing it.
    /// </summary>
    ActorTurn? Peek();

    /// <summary>
    /// Starts executing the next turn, if one is available and the mailbox is idle.
    /// </summary>
    Task<bool> ExecuteNextAsync(CancellationToken cancellationToken = default);
}
