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

namespace Dapr.Actors.Next.Abstractions;

/// <summary>
/// Identifies a Dapr actor instance.
/// </summary>
public readonly record struct ActorId
{
    /// <summary>
    /// Initializes a new actor id.
    /// </summary>
    /// <param name="value">The non-empty actor id value.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null, empty, or whitespace.</exception>
    public ActorId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Actor id cannot be null, empty, or whitespace.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Gets the actor id value used on the wire.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates a new actor id.
    /// </summary>
    public static ActorId Create(string value) => new(value);

    /// <summary>
    /// Parses an actor id from its wire value.
    /// </summary>
    public static ActorId Parse(string value) => new(value);

    /// <summary>
    /// Attempts to parse an actor id from its wire value.
    /// </summary>
    public static bool TryParse(string? value, out ActorId actorId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            actorId = default;
            return false;
        }

        actorId = new ActorId(value);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
