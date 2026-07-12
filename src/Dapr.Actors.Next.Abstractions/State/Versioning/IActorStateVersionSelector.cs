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
/// Defines the policy that selects the latest actor state version from a set of candidates that share the same canonical name.
/// </summary>
public interface IActorStateVersionSelector
{
    /// <summary>
    /// Selects the latest version identity from a non-empty set of candidates.
    /// </summary>
    ActorStateVersionIdentity SelectLatest(
        string canonicalName,
        IReadOnlyCollection<ActorStateVersionIdentity> candidates,
        IActorStateVersionStrategy strategy);
}
