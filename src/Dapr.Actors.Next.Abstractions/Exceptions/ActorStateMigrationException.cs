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
/// Thrown when actor state migration metadata or migration execution fails.
/// </summary>
public sealed class ActorStateMigrationException : ActorStateException
{
    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public ActorStateMigrationException()
    {
    }

    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public ActorStateMigrationException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public ActorStateMigrationException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new exception with migration context.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="familyName">The migration family name, when known.</param>
    /// <param name="chainIndex">The source chain index, when known.</param>
    /// <param name="targetType">The requested target state type, when known.</param>
    /// <param name="shapeHash">The stored state shape hash, when known.</param>
    /// <param name="innerException">The exception that caused this exception, if any.</param>
    public ActorStateMigrationException(
        string? message,
        string? familyName,
        int? chainIndex = null,
        Type? targetType = null,
        string? shapeHash = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        FamilyName = familyName;
        ChainIndex = chainIndex;
        TargetType = targetType;
        ShapeHash = shapeHash;
    }

    /// <summary>
    /// Gets the migration family name, when known.
    /// </summary>
    public string? FamilyName { get; }

    /// <summary>
    /// Gets the source chain index, when known.
    /// </summary>
    public int? ChainIndex { get; }

    /// <summary>
    /// Gets the requested target state type, when known.
    /// </summary>
    public Type? TargetType { get; }

    /// <summary>
    /// Gets the stored state shape hash, when known.
    /// </summary>
    public string? ShapeHash { get; }
}
