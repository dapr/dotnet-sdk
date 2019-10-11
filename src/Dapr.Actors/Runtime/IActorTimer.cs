// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the timer set on an Actor.
    /// </summary>
    public interface IActorTimer
    {
        /// <summary>
        /// Gets the time when timer is first due.
        /// </summary>
        /// <value>Time as <see cref="System.TimeSpan"/> when timer is first due.</value>
        TimeSpan DueTime { get; }

        /// <summary>
        /// Gets the periodic time when timer will be invoked.
        /// </summary>
        /// <value>Periodic time as <see cref="System.TimeSpan"/> when timer will be invoked.</value>
        TimeSpan Period { get; }

        /// <summary>
        /// Gets the name of the Timer. The name is unique per actor.
        /// </summary>
        /// <value>The name of the timer.</value>
        string Name { get; }

        /// <summary>
        /// Gets a delegate that specifies a method to be called when the timer fires.
        /// It has one parameter: the state object passed to RegisterTimer.
        /// It returns a <see cref="System.Threading.Tasks.Task"/> representing the asynchronous operation.
        /// </summary>
        Func<object, Task> AsyncCallback { get; }

        /// <summary>
        /// Gets state containing information to be used by the callback method, or null.
        /// </summary>
        object State { get; }
    }
}
