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

using Dapr.Actors.Next.Abstractions;

namespace Dapr.Actors.Next.Core.Timers;

/// <summary>
/// Describes an actor timer registration.
/// </summary>
/// <param name="ActorType">The actor type that owns the timer.</param>
/// <param name="ActorId">The actor instance that owns the timer.</param>
/// <param name="DueTime">The delay before the first callback, when the registration reports one.</param>
/// <param name="Period">The interval between callbacks, or <see langword="null"/> for a one-shot timer.</param>
/// <param name="ArgumentsJson">The callback payload as JSON text, when one was registered.</param>
/// <param name="ExpiresAt">
/// The absolute UTC instant after which the timer no longer fires, or <see langword="null"/> when it has no TTL.
/// The sidecar reports the expiration computed at registration, so the original TTL duration is not preserved.
/// </param>
/// <param name="OperationName">The actor operation the timer invokes.</param>
public sealed record ActorTimerInfo(
    string ActorType,
    ActorId ActorId,
    TimeSpan? DueTime,
    TimeSpan? Period,
    string? ArgumentsJson,
    DateTimeOffset? ExpiresAt,
    string? OperationName);

/// <summary>
/// Describes a named actor timer registration returned from a list operation.
/// </summary>
public sealed record NamedActorTimerInfo(string Name, ActorTimerInfo Timer);
