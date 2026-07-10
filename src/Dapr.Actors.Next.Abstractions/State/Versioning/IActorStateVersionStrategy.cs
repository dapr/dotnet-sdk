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
/// Defines how actor state type names are parsed into canonical names and versions, and how
/// two version strings are compared for ordering.
/// </summary>
/// <remarks>
/// This compile-time-only input is consumed by generators and analyzers; the runtime consumes resolved
/// migration metadata instead.
/// </remarks>
public interface IActorStateVersionStrategy : IComparer<string>
{
    /// <summary>
    /// Attempts to derive a canonical family name and version from an actor state type name.
    /// </summary>
    bool TryParse(string typeName, out string canonicalName, out string version);

    /// <summary>
    /// Compares two version strings and returns their relative order.
    /// </summary>
    new int Compare(string v1, string v2);
}
