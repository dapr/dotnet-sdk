// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

namespace Dapr.Actors.Runtime;

/// <summary>
/// Represents the call-type associated with the method invoked by actor runtime.
/// </summary>
/// <remarks>
/// This is provided as part of <see cref="ActorMethodContext"/> which is passed as argument to
/// <see cref="Actor.OnPreActorMethodAsync"/> and <see cref="Actor.OnPostActorMethodAsync"/>.
/// </remarks>
public enum ActorCallType
{
    /// <summary>
    /// Specifies that the method invoked is an actor interface method for a given client request.
    /// </summary>
    ActorInterfaceMethod = 0,

    /// <summary>
    /// Specifies that the method invoked is a timer callback method.
    /// </summary>
    TimerMethod = 1,

    /// <summary>
    /// Specifies that the method is when a reminder fires.
    /// </summary>
    ReminderMethod = 2,
}