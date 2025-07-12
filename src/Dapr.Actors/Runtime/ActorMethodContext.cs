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
/// Contains information about the method that is invoked by actor runtime and
/// is passed as an argument to <see cref="Actor.OnPreActorMethodAsync"/> and <see cref="Actor.OnPostActorMethodAsync"/>.
/// </summary>
public struct ActorMethodContext
{
    private readonly string actorMethodName;
    private readonly ActorCallType actorCallType;

    private ActorMethodContext(string methodName, ActorCallType callType)
    {
        this.actorMethodName = methodName;
        this.actorCallType = callType;
    }

    /// <summary>
    /// Gets the name of the method invoked by actor runtime.
    /// </summary>
    /// <value>The name of method.</value>
    public string MethodName
    {
        get { return this.actorMethodName; }
    }

    /// <summary>
    /// Gets the type of call by actor runtime (e.g. actor interface method, timer callback etc.).
    /// </summary>
    /// <value>
    /// An <see cref="ActorCallType"/> representing the call type.
    /// </value>
    public ActorCallType CallType
    {
        get { return this.actorCallType; }
    }

    internal static ActorMethodContext CreateForActor(string methodName)
    {
        return new ActorMethodContext(methodName, ActorCallType.ActorInterfaceMethod);
    }

    internal static ActorMethodContext CreateForTimer(string methodName)
    {
        return new ActorMethodContext(methodName, ActorCallType.TimerMethod);
    }

    internal static ActorMethodContext CreateForReminder(string methodName)
    {
        return new ActorMethodContext(methodName, ActorCallType.ReminderMethod);
    }
}