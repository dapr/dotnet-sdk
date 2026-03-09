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
/// Default messages for diagnostics produced by the versioning generator and runtime.
/// Provides stable titles and formatted strings suitable for logging or analyzers.
/// </summary>
public sealed class DefaultWorkflowVersioningDiagnostics : IWorkflowVersionDiagnostics
{
    /// <inheritdoc />
    public string UnknownStrategyTitle => "Invalid workflow versioning strategy";
    
    /// <inheritdoc />
    public string UnknownStrategyMessage(string typeName, Type strategyType)
    {
        if (string.IsNullOrEmpty(typeName))
            typeName = "<unknown>";

        var strategy = strategyType?.FullName ?? "<null>";
        return $"The strategy type '{strategy}' specified for workflow type '{typeName}' could not be constructed " +
               "or does not implemenet IWorkflowVersionStrategy";
    }

    /// <inheritdoc />
    public string CouldNotParseTitle => "Unable to derive canonical name and version";
    
    /// <inheritdoc />
    public string CouldNotParseMessage(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            typeName = "<unknown>";

        return
            $"No available strategy could parse the workflow type name '{typeName}' into a canonical name and version";
    }

    /// <inheritdoc />
    public string EmptyFamilyTitle => "No versions discovered for workflow family";
    
    /// <inheritdoc />
    public string EmptyFamilyMessage(string canonicalName)
    {
        if (string.IsNullOrWhiteSpace(canonicalName))
            canonicalName = "<unknown>";

        return $"No versions were discovered for the canonical workflow family '{canonicalName}'.";
    }

    /// <inheritdoc />
    public string AmbiguousLatestTitle => "Ambiguous latest workflow version";
    
    /// <inheritdoc />
    public string AmbiguousLatestMessage(string canonicalName, IReadOnlyList<string>? versions)
    {
        if (string.IsNullOrWhiteSpace(canonicalName))
            canonicalName = "<unknown>";

        var hasAny = versions is not null && (versions as ICollection<string>)?.Count > 0 || (versions?.Any() ?? false);
        var list = hasAny ? string.Join(", ", versions!) : "<none>";
        return $"Multiple versions for '{canonicalName}' are tied for latest: [{list}].";
    }
}
