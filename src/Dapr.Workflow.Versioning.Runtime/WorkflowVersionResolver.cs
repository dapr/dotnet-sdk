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
/// Default implementation of <see cref="IWorkflowVersionResolver"/> that consults the configured
/// <see cref="IWorkflowVersionStrategy"/> and <see cref="IWorkflowVersionSelector"/>.
/// </summary>
/// <param name="services">Application service provider.</param>
/// <param name="options">The versioning options.</param>
/// <param name="diagnostics">Diagnostic messages provider.</param>
public sealed class WorkflowVersionResolver(
    IServiceProvider services,
    WorkflowVersioningOptions options,
    IWorkflowVersionDiagnostics diagnostics)
    : IWorkflowVersionResolver
{
    private readonly IServiceProvider _services = services ?? throw new ArgumentNullException(nameof(services));
    private readonly WorkflowVersioningOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly IWorkflowVersionDiagnostics _diag = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));

    /// <inheritdoc />
    public bool TryGetLatest(WorkflowFamily family, out WorkflowVersionIdentity latest, out string? diagnosticId,
        out string? diagnosticMessage)
    {
        latest = default;
        diagnosticId = null;
        diagnosticMessage = null;
        
        ArgumentNullException.ThrowIfNull(family);
        if (family.Versions is null || family.Versions.Count == 0)
        {
            diagnosticId = IWorkflowVersioningDiagnosticIds.EmptyFamily;
            diagnosticMessage = _diag.EmptyFamilyMessage(family.CanonicalName);
            return false;
        }
        
        // Resolve strategy and selector (global defaults)
        var strategy = _options.DefaultStrategy?.Invoke(_services)
                       ?? throw new InvalidOperationException("No default workflow versioning strategy is configured.");
        var selector = _options.DefaultSelector?.Invoke(_services) ?? new MaxVersionSelector();
        
        // Select latest
        try
        {
            latest = selector.SelectLatest(family.CanonicalName, family.Versions, strategy);
            return true;
        }
        catch (ArgumentException)
        {
            diagnosticId = IWorkflowVersioningDiagnosticIds.EmptyFamily;
            diagnosticMessage = _diag.EmptyFamilyMessage(family.CanonicalName);
            return false;
        }
        catch (InvalidOperationException)
        {
            var tied = family.Versions
                .GroupBy(v => v.Version)
                .OrderByDescending(g => g.Key, strategy)
                .FirstOrDefault();

            diagnosticId = IWorkflowVersioningDiagnosticIds.AmbiguousLatest;
            diagnosticMessage = _diag.AmbiguousLatestMessage(family.CanonicalName, tied?.Select(v => v.Version).ToList() ?? []);
            return false;
        }
    }
}
