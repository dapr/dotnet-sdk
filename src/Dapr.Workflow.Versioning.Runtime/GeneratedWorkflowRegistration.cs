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

namespace Dapr.Workflow.Versioning;

/// <summary>
/// Describes a source-generated workflow contribution. Intended for use by generated code.
/// </summary>
public sealed class GeneratedWorkflowRegistration
{
    /// <summary>Creates a workflow contribution without runtime reflection over workflow attributes.</summary>
    /// <param name="workflowType">The concrete workflow type.</param>
    /// <param name="workflowTypeName">The fully qualified registration name.</param>
    /// <param name="canonicalName">The optional declared family name.</param>
    /// <param name="version">The optional declared version.</param>
    /// <param name="strategyType">The optional family strategy override.</param>
    /// <param name="optionsName">The optional strategy options name.</param>
    /// <param name="register">Registers the workflow under a supplied name.</param>
    public GeneratedWorkflowRegistration(Type workflowType, string workflowTypeName, string? canonicalName,
        string? version, Type? strategyType, string? optionsName, Action<WorkflowRuntimeOptions, string> register)
    {
        ArgumentNullException.ThrowIfNull(workflowType);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowTypeName);
        ArgumentNullException.ThrowIfNull(register);
        WorkflowType = workflowType;
        WorkflowTypeName = workflowTypeName;
        CanonicalName = canonicalName;
        Version = version;
        StrategyType = strategyType;
        OptionsName = optionsName;
        Register = register;
    }

    internal Type WorkflowType { get; }
    internal string WorkflowTypeName { get; }
    internal string? CanonicalName { get; }
    internal string? Version { get; }
    internal Type? StrategyType { get; }
    internal string? OptionsName { get; }
    internal Action<WorkflowRuntimeOptions, string> Register { get; }
}
