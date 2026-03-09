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

using System;
using System.Collections.Generic;

namespace Dapr.Workflow.Versioning;

/// <summary>
/// Default routing registry for workflow name resolution.
/// </summary>
public sealed class WorkflowRouterRegistry : IWorkflowRouterRegistry
{
    private readonly object _sync = new();
    private Dictionary<string, IReadOnlyList<string>> _routes = new(StringComparer.Ordinal);
    private HashSet<string> _knownNames = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void UpdateRoutes(IReadOnlyDictionary<string, IReadOnlyList<string>> routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var updatedRoutes = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var updatedNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var kvp in routes)
        {
            updatedRoutes[kvp.Key] = kvp.Value;
            updatedNames.Add(kvp.Key);

            foreach (var name in kvp.Value)
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    updatedNames.Add(name);
                }
            }
        }

        lock (_sync)
        {
            _routes = updatedRoutes;
            _knownNames = updatedNames;
        }
    }

    /// <inheritdoc />
    public bool Contains(string workflowName)
    {
        if (string.IsNullOrWhiteSpace(workflowName))
            return false;

        lock (_sync)
        {
            return _knownNames.Contains(workflowName);
        }
    }

    /// <inheritdoc />
    public bool TryResolveLatest(out string workflowName)
    {
        lock (_sync)
        {
            workflowName = string.Empty;

            if (_routes.Count != 1)
            {
                return false;
            }

            foreach (var kvp in _routes)
            {
                if (kvp.Value.Count == 0)
                {
                    return false;
                }

                workflowName = kvp.Value[0];
                return !string.IsNullOrWhiteSpace(workflowName);
            }

            return false;
        }
    }
}
