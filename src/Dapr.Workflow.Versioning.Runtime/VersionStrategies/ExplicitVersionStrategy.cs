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

using Microsoft.Extensions.Options;

namespace Dapr.Workflow.Versioning;

/// <summary>
/// Strategy that requires versions to be supplied explicitly via <see cref="WorkflowVersionAttribute"/>.
/// </summary>
public sealed class ExplicitVersionStrategy(IOptionsMonitor<ExplicitVersionStrategyOptions>? optionsMonitor = null)
    : IWorkflowVersionStrategy, IWorkflowVersionStrategyContextConsumer
{
    private ExplicitVersionStrategyOptions _options = new();

    /// <inheritdoc />
    public void Configure(WorkflowVersionStrategyContext context)
    {
        var optionsName = string.IsNullOrWhiteSpace(context.OptionsName)
            ? Options.DefaultName
            : context.OptionsName;

        if (optionsMonitor is not null)
        {
            _options = optionsMonitor.Get(optionsName);
        }
    }

    /// <inheritdoc />
    public bool TryParse(string typeName, out string canonicalName, out string version)
    {
        canonicalName = string.Empty;
        version = string.Empty;

        if (!_options.AllowMissingVersion || string.IsNullOrWhiteSpace(typeName))
            return false;

        canonicalName = typeName;
        version = string.IsNullOrWhiteSpace(_options.DefaultVersion) ? "0" : _options.DefaultVersion;
        return true;
    }

    /// <inheritdoc />
    public int Compare(string? v1, string? v2)
    {
        if (ReferenceEquals(v1, v2)) return 0;
        if (v1 is null) return -1;
        if (v2 is null) return 1;

        var comparer = _options.IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        return comparer.Compare(v1, v2);
    }
}
