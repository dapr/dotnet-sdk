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
/// Standard diagnostic IDs provides by the workflow versioning generator and runtime.
/// </summary>
/// <remarks>
/// These IDs are intentionally stable and can be used for filtering or documentation.
/// </remarks>
public static class IWorkflowVersioningDiagnosticIds
{
    /// <summary>
    /// Diagnostic with a <see cref="WorkflowVersionAttribute.StrategyType"/> cannot be instantiated or does not
    /// implement <see cref="IWorkflowVersionStrategy"/>.
    /// </summary>
    public const string UnknownStrategy = "DWV001";

    /// <summary>
    /// Diagnostic with no strategy can parse the workflow type name into a canonical name and version.
    /// </summary>
    public const string CouldNotParse = "DWV002";

    /// <summary>
    /// Diagnostic when a canonical family has no versions (e.g., all were filtered out).
    /// </summary>
    public const string EmptyFamily = "DWV003";

    /// <summary>
    /// Diagnostic when selection policy cannot determine a unique latest version (ambiguous winners).
    /// </summary>
    public const string AmbiguousLatest = "DWV004";
}
