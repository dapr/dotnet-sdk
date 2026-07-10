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
/// Production scheduler that drains each actor mailbox one turn at a time.
/// </summary>
public sealed class ProductionActorScheduler : IActorScheduler
{
    /// <inheritdoc />
    public bool AllowsInlineExecution => true;

    /// <inheritdoc />
    public ValueTask ScheduleAsync(IActorMailbox mailbox, CancellationToken cancellationToken = default)
    {
        if (mailbox is RuntimeActorMailbox runtimeMailbox && runtimeMailbox.TryScheduleDrain(cancellationToken))
        {
            _ = Task.Run(runtimeMailbox.DrainScheduledAsync, CancellationToken.None);
        }

        return ValueTask.CompletedTask;
    }
}
