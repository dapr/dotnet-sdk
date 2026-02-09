// ------------------------------------------------------------------------
//  Copyright 2026 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

namespace Dapr.Workflow.Versioning;

/// <summary>
/// Defines how workflow type names are parsed into canonical names and versions, and how
/// two version strings are compared for ordering.
/// </summary>
/// <remarks>
/// Implementations may encode numeric suffix rules, prefix rules, SemVer, date-based versions, etc.
/// This interface also implements <see cref="IComparer{T}"/> for comparing version strings directly.
/// </remarks>
public interface IWorkflowVersionStrategy : IComparer<string>
{
    /// <summary>
    /// Attempts to derive a canonical name and version from a workflow type name.
    /// Returns true on success; false if this strategy cannot parse the name.
    /// </summary>
    /// <param name="typeName">The workflow type name to parse.</param>
    /// <param name="canonicalName">When successful, receives the canonical name for the workflow family.</param>
    /// <param name="version">When successful, receives the parsed version string.</param>
    /// <returns>
    /// <see langword="true"/> if the strategy could derive a canonical name and version;
    /// otherwise <see langword="false"/>.
    /// </returns>
    bool TryParse(string typeName, out string canonicalName, out string version);

    /// <summary>
    /// Compares two version strings and returns a value indicating their relative order.
    /// </summary>
    /// <param name="v1">The first version string.</param>
    /// <param name="v2">The second version string.</param>
    /// <returns>
    /// A signed integer that indicates the relative values of <paramref name="v1"/> and <paramref name="v2"/>:
    /// - Less than zero if <paramref name="v1"/> is older than <paramref name="v2"/>;
    /// - Zero if they are equal;
    /// - Greater than zero if <paramref name="v1"/> is newer than <paramref name="v2"/>.
    /// </returns>
    new int Compare(string v1, string v2);
}
