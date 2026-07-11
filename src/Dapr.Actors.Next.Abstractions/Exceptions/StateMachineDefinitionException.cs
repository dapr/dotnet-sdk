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
/// Thrown when a state-machine actor definition is invalid or cannot be executed by the state-machine runtime.
/// </summary>
public sealed class StateMachineDefinitionException : DaprActorException
{
    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public StateMachineDefinitionException()
    {
    }

    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public StateMachineDefinitionException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public StateMachineDefinitionException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new exception with state-machine definition context.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="actorType">The actor implementation type, when known.</param>
    /// <param name="stateName">The state name involved in the definition failure, when known.</param>
    /// <param name="innerException">The exception that caused this exception, if any.</param>
    public StateMachineDefinitionException(string? message, Type? actorType, string? stateName = null, Exception? innerException = null)
        : base(message, innerException)
    {
        ActorType = actorType;
        StateName = stateName;
    }

    /// <summary>
    /// Gets the actor implementation type, when known.
    /// </summary>
    public Type? ActorType { get; }

    /// <summary>
    /// Gets the state name involved in the definition failure, when known.
    /// </summary>
    public string? StateName { get; }
}
