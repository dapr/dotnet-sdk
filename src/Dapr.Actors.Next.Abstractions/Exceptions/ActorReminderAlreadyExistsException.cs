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

namespace Dapr.Actors.Next.Abstractions.Exceptions;

/// <summary>
/// Thrown when a durable actor reminder already exists and the registration request does not allow overwriting it.
/// </summary>
public sealed class ActorReminderAlreadyExistsException : DaprActorException
{
    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public ActorReminderAlreadyExistsException()
    {
    }

    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public ActorReminderAlreadyExistsException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public ActorReminderAlreadyExistsException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new exception for the conflicting reminder.
    /// </summary>
    /// <param name="actorType">The actor type that owns the reminder.</param>
    /// <param name="actorId">The actor id that owns the reminder.</param>
    /// <param name="reminderName">The duplicate reminder name.</param>
    /// <param name="innerException">The exception that caused this exception, if any.</param>
    public ActorReminderAlreadyExistsException(string actorType, string actorId, string reminderName, Exception? innerException = null)
        : base($"Reminder '{reminderName}' is already scheduled for actor '{actorType}/{actorId}'.", innerException)
    {
        ActorType = actorType;
        ActorId = actorId;
        ReminderName = reminderName;
    }

    /// <summary>
    /// Gets the actor type that owns the reminder, when known.
    /// </summary>
    public string? ActorType { get; }

    /// <summary>
    /// Gets the actor id that owns the reminder, when known.
    /// </summary>
    public string? ActorId { get; }

    /// <summary>
    /// Gets the duplicate reminder name, when known.
    /// </summary>
    public string? ReminderName { get; }
}
