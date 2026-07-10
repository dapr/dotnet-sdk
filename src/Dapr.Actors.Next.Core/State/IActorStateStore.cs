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

namespace Dapr.Actors.Next.Core.State;

/// <summary>
/// Persists actor state envelopes for Core.
/// </summary>
public interface IActorStateStore
{
    /// <summary>
    /// Reads a state value.
    /// </summary>
    ValueTask<ReadOnlyMemory<byte>?> ReadAsync(string actorType, string actorId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a state value.
    /// </summary>
    ValueTask WriteAsync(string actorType, string actorId, string name, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a state value.
    /// </summary>
    ValueTask DeleteAsync(string actorType, string actorId, string name, CancellationToken cancellationToken = default);
}
