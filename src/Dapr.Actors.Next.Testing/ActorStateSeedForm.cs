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

namespace Dapr.Actors.Next.Testing;

/// <summary>
/// Selects the persisted form written by <see cref="ActorTestRuntime.SeedStateAsync{T}(string, Dapr.Actors.Next.Abstractions.ActorId, string, T, ActorStateSeedForm, CancellationToken)"/>.
/// </summary>
public enum ActorStateSeedForm
{
    /// <summary>
    /// Seed an enrolled state value with the migration discriminator for the seeded value's type.
    /// </summary>
    Enveloped,

    /// <summary>
    /// Seed a graduated plain state value with the SDK state header and no migration discriminator.
    /// </summary>
    Plain,
}
