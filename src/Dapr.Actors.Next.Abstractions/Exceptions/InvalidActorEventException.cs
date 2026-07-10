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
/// Thrown when an event is invalid for an actor state machine state.
/// </summary>
public sealed class InvalidActorEventException : DaprActorException
{
    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public InvalidActorEventException()
    {
    }

    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public InvalidActorEventException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public InvalidActorEventException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new exception from the state and event that failed.
    /// </summary>
    public InvalidActorEventException(object? state, object? actorEvent)
        : base($"Event '{actorEvent}' is invalid for actor state '{state}'.")
    {
        StateName = state?.ToString();
        EventName = actorEvent?.GetType().FullName ?? actorEvent?.ToString();
    }

    /// <summary>
    /// Gets the state name associated with the invalid event.
    /// </summary>
    public string? StateName { get; }

    /// <summary>
    /// Gets the event name associated with the invalid event.
    /// </summary>
    public string? EventName { get; }
}
