// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

namespace Dapr.VirtualActors;

/// <summary>
/// Represents the unique identity of an actor within its type.
/// </summary>
/// <remarks>
/// <para>
/// Actor IDs are case-sensitive string values that uniquely identify an actor instance
/// within a given actor type. When combined with the actor type, the actor ID forms a
/// globally unique address for the actor.
/// </para>
/// <para>
/// This type is immutable and safe for use as a dictionary key.
/// </para>
/// </remarks>
public readonly struct VirtualActorId : IEquatable<VirtualActorId>, IComparable<VirtualActorId>
{
    private readonly string _id;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualActorId"/> struct.
    /// </summary>
    /// <param name="id">The string value representing the identity of the actor.</param>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="id"/> is <see langword="null"/> or whitespace.
    /// </exception>
    public VirtualActorId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        _id = id;
    }

    /// <summary>
    /// Gets the string representation of this actor identity.
    /// </summary>
    /// <returns>The string ID of this actor.</returns>
    public string GetId() => _id;

    /// <inheritdoc />
    public bool Equals(VirtualActorId other) => string.Equals(_id, other._id, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is VirtualActorId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _id?.GetHashCode(StringComparison.Ordinal) ?? 0;

    /// <inheritdoc />
    public int CompareTo(VirtualActorId other) => string.Compare(_id, other._id, StringComparison.Ordinal);

    /// <inheritdoc />
    public override string ToString() => _id;

    /// <summary>
    /// Determines whether two <see cref="VirtualActorId"/> instances are equal.
    /// </summary>
    public static bool operator ==(VirtualActorId left, VirtualActorId right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="VirtualActorId"/> instances are not equal.
    /// </summary>
    public static bool operator !=(VirtualActorId left, VirtualActorId right) => !left.Equals(right);

    /// <summary>
    /// Determines whether one <see cref="VirtualActorId"/> is less than another.
    /// </summary>
    public static bool operator <(VirtualActorId left, VirtualActorId right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Determines whether one <see cref="VirtualActorId"/> is less than or equal to another.
    /// </summary>
    public static bool operator <=(VirtualActorId left, VirtualActorId right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Determines whether one <see cref="VirtualActorId"/> is greater than another.
    /// </summary>
    public static bool operator >(VirtualActorId left, VirtualActorId right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Determines whether one <see cref="VirtualActorId"/> is greater than or equal to another.
    /// </summary>
    public static bool operator >=(VirtualActorId left, VirtualActorId right) => left.CompareTo(right) >= 0;
}
