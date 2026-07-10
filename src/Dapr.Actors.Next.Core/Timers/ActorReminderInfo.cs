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
/// Describes a durable actor reminder registration.
/// </summary>
public sealed record ActorReminderInfo(
    string ActorType,
    ActorId ActorId,
    TimeSpan? DueTime,
    TimeSpan? Period,
    string? ArgumentsJson,
    TimeSpan? Ttl);

/// <summary>
/// Describes a named durable actor reminder registration returned from a list operation.
/// </summary>
public sealed record NamedActorReminderInfo(string Name, ActorReminderInfo Reminder);
