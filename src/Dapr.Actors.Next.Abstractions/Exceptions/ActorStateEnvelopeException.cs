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
/// Thrown when persisted actor state uses an unsupported or incompatible SDK state envelope.
/// </summary>
public sealed class ActorStateEnvelopeException : ActorStateException
{
    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public ActorStateEnvelopeException()
    {
    }

    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public ActorStateEnvelopeException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public ActorStateEnvelopeException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new exception with envelope context.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="stateName">The persisted state name, when known.</param>
    /// <param name="formatVersion">The envelope format version, when known.</param>
    /// <param name="formKind">The envelope form kind, when known.</param>
    /// <param name="storedSerializerId">The serializer id stored in the envelope, when known.</param>
    /// <param name="storedSerializerVersion">The serializer version stored in the envelope, when known.</param>
    /// <param name="currentSerializerId">The currently configured serializer id, when known.</param>
    /// <param name="currentSerializerVersion">The currently configured serializer version, when known.</param>
    /// <param name="innerException">The exception that caused this exception, if any.</param>
    public ActorStateEnvelopeException(
        string? message,
        string? stateName,
        int? formatVersion = null,
        string? formKind = null,
        string? storedSerializerId = null,
        int? storedSerializerVersion = null,
        string? currentSerializerId = null,
        int? currentSerializerVersion = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StateName = stateName;
        FormatVersion = formatVersion;
        FormKind = formKind;
        StoredSerializerId = storedSerializerId;
        StoredSerializerVersion = storedSerializerVersion;
        CurrentSerializerId = currentSerializerId;
        CurrentSerializerVersion = currentSerializerVersion;
    }

    /// <summary>
    /// Gets the persisted state name, when known.
    /// </summary>
    public string? StateName { get; }

    /// <summary>
    /// Gets the envelope format version, when known.
    /// </summary>
    public int? FormatVersion { get; }

    /// <summary>
    /// Gets the envelope form kind, when known.
    /// </summary>
    public string? FormKind { get; }

    /// <summary>
    /// Gets the serializer id stored in the envelope, when known.
    /// </summary>
    public string? StoredSerializerId { get; }

    /// <summary>
    /// Gets the serializer version stored in the envelope, when known.
    /// </summary>
    public int? StoredSerializerVersion { get; }

    /// <summary>
    /// Gets the currently configured serializer id, when known.
    /// </summary>
    public string? CurrentSerializerId { get; }

    /// <summary>
    /// Gets the currently configured serializer version, when known.
    /// </summary>
    public int? CurrentSerializerVersion { get; }
}
