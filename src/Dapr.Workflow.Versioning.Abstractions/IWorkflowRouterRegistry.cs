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

using System.Collections.Generic;

namespace Dapr.Workflow.Versioning;

/// <summary>
/// Provides a simple routing registry for resolving workflow names.
/// </summary>
public interface IWorkflowRouterRegistry
{
    /// <summary>
    /// Updates the routing map using canonical workflow names and ordered workflow names.
    /// </summary>
    /// <param name="routes">Canonical name to ordered workflow names (latest first).</param>
    void UpdateRoutes(IReadOnlyDictionary<string, IReadOnlyList<string>> routes);

    /// <summary>
    /// Returns true if the registry contains the supplied workflow name.
    /// </summary>
    bool Contains(string workflowName);

    /// <summary>
    /// Attempts to resolve the latest workflow name when no history name is available.
    /// </summary>
    bool TryResolveLatest(out string workflowName);
}
