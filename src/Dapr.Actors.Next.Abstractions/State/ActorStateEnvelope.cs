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

namespace Dapr.Actors.Next.Abstractions.State;

/// <summary>
/// Identifies the persisted actor state payload form.
/// </summary>
public enum ActorStateFormKind : byte
{
    /// <summary>
    /// The payload is enrolled in a migration chain and carries a discriminator.
    /// </summary>
    Enveloped = 1,

    /// <summary>
    /// The payload is stored without a migration discriminator.
    /// </summary>
    Plain = 2,
}

/// <summary>
/// The fixed header read before every SDK-written actor state payload.
/// </summary>
/// <param name="Magic">The content-independent format tag.</param>
/// <param name="FormatVersion">The actor state envelope format version.</param>
/// <param name="FormKind">The logical payload form.</param>
/// <param name="SerializerId">The serializer that wrote the payload.</param>
/// <param name="SerializerVersion">The serializer format or configuration version.</param>
public readonly record struct ActorStateEnvelopeHeader(
    byte Magic,
    int FormatVersion,
    ActorStateFormKind FormKind,
    string SerializerId,
    int SerializerVersion)
{
    /// <summary>
    /// The current non-JSON magic byte used to identify SDK-written actor state.
    /// </summary>
    public const byte CurrentMagic = 0xD1;

    /// <summary>
    /// The current actor state envelope format version.
    /// </summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>
    /// Creates a header for the current actor state envelope format.
    /// </summary>
    public static ActorStateEnvelopeHeader Create(
        ActorStateFormKind formKind,
        string serializerId,
        int serializerVersion) =>
        new(CurrentMagic, CurrentFormatVersion, formKind, serializerId, serializerVersion);
}

/// <summary>
/// Identifies the versioned state node that wrote an enrolled payload.
/// </summary>
/// <param name="ChainIndex">The node position in its family's migration chain.</param>
/// <param name="ShapeHash">The algorithm-versioned structural hash for the node, for example <c>h1:...</c>.</param>
public readonly record struct ActorStateDiscriminator(int ChainIndex, string ShapeHash)
{
    /// <summary>
    /// The current shape-hash algorithm prefix.
    /// </summary>
    public const string CurrentShapeHashPrefix = "h1:";
}

/// <summary>
/// The persisted actor state envelope for an enrolled, versioned payload.
/// </summary>
/// <param name="Header">The payload header.</param>
/// <param name="Discriminator">The migration-chain discriminator.</param>
/// <param name="Value">The typed payload.</param>
public sealed record ActorStateEnvelope<T>(
    ActorStateEnvelopeHeader Header,
    ActorStateDiscriminator Discriminator,
    T Value);

/// <summary>
/// The persisted actor state envelope for a plain payload.
/// </summary>
/// <param name="Header">The payload header.</param>
/// <param name="Value">The typed payload.</param>
public sealed record ActorStatePlainEnvelope<T>(
    ActorStateEnvelopeHeader Header,
    T Value);
