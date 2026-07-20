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

namespace Dapr.Actors.Next.Core.Timers;

/// <summary>
/// Describes the policy to apply when an actor reminder fails to trigger.
/// </summary>
public abstract record ActorReminderFailurePolicy
{
    private ActorReminderFailurePolicy()
    {
    }

    /// <summary>
    /// Drops the reminder tick when it fails to trigger.
    /// </summary>
    public sealed record Drop : ActorReminderFailurePolicy;

    /// <summary>
    /// Retries the reminder tick at a consistent interval when it fails to trigger.
    /// </summary>
    /// <param name="Interval">The constant delay to wait before retrying the reminder tick.</param>
    public sealed record Constant(TimeSpan Interval) : ActorReminderFailurePolicy
    {
        /// <summary>
        /// Gets the optional maximum number of retries to attempt before giving up. If unset, retries continue indefinitely.
        /// </summary>
        public uint? MaxRetries { get; init; }
    }
}
