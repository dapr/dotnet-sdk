// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface that actors must implement to consume reminders registered using RegisterReminderAsync. />.
    /// </summary>
    public interface IRemindable
    {
        /// <summary>
        /// The reminder call back invoked when an actor reminder is triggered.
        /// </summary>
        /// <param name="reminderName">The name of reminder provided during registration.</param>
        /// <param name="state">The user state provided during registration.</param>
        /// <param name="dueTime">The invocation due time provided during registration.</param>
        /// <param name="period">The invocation period provided during registration.</param>
        /// <returns>A task that represents the asynchronous operation performed by this callback.</returns>
        /// <remarks>
        /// <para>The state of this actor is saved by the actor runtime upon completion of the task returned by this method. If an error occurs while saving the state, then all state cached by this actor's <see cref="Dapr.Actors.Runtime.Actor.StateManager" /> will be discarded and reloaded from previously saved state when the next actor method or reminder invocation occurs.
        /// </para>
        /// </remarks>
        Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period);
    }
}
