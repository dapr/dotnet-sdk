// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
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
}
