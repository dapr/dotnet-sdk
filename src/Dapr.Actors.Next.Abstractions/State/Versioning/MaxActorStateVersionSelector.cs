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

namespace Dapr.Actors.Next.Abstractions.State.Versioning;

/// <summary>
/// Default selector that chooses the maximum version according to the active strategy.
/// </summary>
public sealed class MaxActorStateVersionSelector : IActorStateVersionSelector
{
    /// <inheritdoc />
    public ActorStateVersionIdentity SelectLatest(
        string canonicalName,
        IReadOnlyCollection<ActorStateVersionIdentity> candidates,
        IActorStateVersionStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(strategy);
        ArgumentOutOfRangeException.ThrowIfEqual(0, candidates.Count, nameof(candidates));

        return candidates.OrderBy(candidate => candidate.Version, strategy).Last();
    }
}
