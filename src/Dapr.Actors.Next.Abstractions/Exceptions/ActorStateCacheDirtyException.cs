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
/// Thrown when actor state cache eviction is rejected because cached state has unpersisted changes.
/// </summary>
public sealed class ActorStateCacheDirtyException : ActorStateException
{
    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public ActorStateCacheDirtyException()
    {
    }

    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public ActorStateCacheDirtyException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public ActorStateCacheDirtyException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new exception for a dirty cached state value.
    /// </summary>
    /// <param name="stateName">The cached state name with unpersisted changes.</param>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The exception that caused this exception, if any.</param>
    public ActorStateCacheDirtyException(string stateName, string? message, Exception? innerException = null)
        : base(message, innerException)
    {
        StateName = stateName;
    }

    /// <summary>
    /// Gets the cached state name with unpersisted changes, when known.
    /// </summary>
    public string? StateName { get; }
}
