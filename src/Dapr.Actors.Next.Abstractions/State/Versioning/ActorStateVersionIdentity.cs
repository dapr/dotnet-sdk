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
/// Identifies a single actor state version within a canonical family.
/// </summary>
/// <param name="CanonicalName">The canonical family name.</param>
/// <param name="Version">The strategy-defined version string.</param>
/// <param name="TypeName">The CLR type name that implements this actor state version.</param>
/// <param name="AssemblyName">Optional assembly name that contains the state type.</param>
public readonly record struct ActorStateVersionIdentity(
    string CanonicalName,
    string Version,
    string TypeName,
    string? AssemblyName = null)
{
    /// <inheritdoc />
    public override string ToString() => $"{CanonicalName}@{Version} ({TypeName})";
}
