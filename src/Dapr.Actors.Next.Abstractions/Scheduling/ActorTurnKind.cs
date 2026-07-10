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
/// Identifies the kind of actor turn.
/// </summary>
public enum ActorTurnKind
{
    /// <summary>
    /// A normal actor method invocation.
    /// </summary>
    Invoke,

    /// <summary>
    /// A reminder callback.
    /// </summary>
    Reminder,

    /// <summary>
    /// A timer callback.
    /// </summary>
    Timer,

    /// <summary>
    /// A deactivation callback.
    /// </summary>
    Deactivate,

    /// <summary>
    /// A pub/sub event forwarded to an actor.
    /// </summary>
    Subscription,
}
