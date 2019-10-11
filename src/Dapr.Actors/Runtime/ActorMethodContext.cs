// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
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
}
