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

namespace Dapr.VirtualActors.Runtime;

/// <summary>
/// A no-op timer and reminder manager used for unit testing.
/// </summary>
/// <remarks>
/// All operations complete successfully but have no observable effect.
/// Provide a mock implementation when you need to verify timer/reminder calls.
/// </remarks>
public sealed class NoOpActorTimerManager : IActorTimerManager
{
    /// <inheritdoc />
    public Task RegisterTimerAsync(
        string actorType, VirtualActorId actorId, string timerName,
        string callbackMethodName, byte[]? callbackData,
        TimeSpan dueTime, TimeSpan period, TimeSpan? ttl = null,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task UnregisterTimerAsync(
        string actorType, VirtualActorId actorId, string timerName,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RegisterReminderAsync(
        string actorType, VirtualActorId actorId, string reminderName,
        byte[]? data, TimeSpan dueTime, TimeSpan period, TimeSpan? ttl = null,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task UnregisterReminderAsync(
        string actorType, VirtualActorId actorId, string reminderName,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
