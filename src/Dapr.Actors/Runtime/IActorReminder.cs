// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System;

    /// <summary>
    /// Represents a reminder registered using <see cref="Dapr.Actors.Runtime.Actor.RegisterReminderAsync" />.
    /// </summary>
    public interface IActorReminder
    {
        /// <summary>
        /// Gets the name of the reminder. The name is unique per actor.
        /// </summary>
        /// <value>The name of the reminder.</value>
        string Name { get; }

        /// <summary>
        /// Gets the time when the reminder is first due to be invoked.
        /// </summary>
        /// <value>The time when the reminder is first due to be invoked.</value>
        /// <remarks>
        /// A value of negative one (-1) milliseconds means the reminder is not invoked. A value of zero (0) means the reminder is invoked immediately after registration.
        /// </remarks>
        TimeSpan DueTime { get; }

        /// <summary>
        /// Gets the time interval at which the reminder is invoked periodically.
        /// </summary>
        /// <value>The time interval at which the reminder is invoked periodically.</value>
        /// <remarks>
        /// The first invocation of the reminder occurs after <see cref="Dapr.Actors.Runtime.IActorReminder.DueTime" />. All subsequent invocations occur at intervals defined by this property.
        /// </remarks>
        TimeSpan Period { get; }

        /// <summary>
        /// Gets the user state passed to the reminder invocation.
        /// </summary>
        /// <value>The user state passed to the reminder invocation.</value>
        byte[] State { get; }
    }
}
