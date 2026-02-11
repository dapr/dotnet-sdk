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
/// Declares versioning metadata for a workflow type. Apply this attribute to a Dapr Workflow class
/// to override the canonical name, supply an explicit version string, and/or specify a per-type versioning
/// strategy.
/// </summary>
/// <remarks>
/// If <see cref="CanonicalName"/> and <see cref="Version"/> are not provided, the active
/// <see cref="IWorkflowVersionStrategy"/> is responsible for deriving them from the type name.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class WorkflowVersionAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the canonical name that identifies all versions of this workflow family (for
    /// example <c>"OrderProcessingWorkflow"</c>. When omitted, the strategy derives it from the
    /// type name.
    /// </summary>
    public string? CanonicalName { get; init; }
    
    /// <summary>
    /// Gets or sets an explicit version string for this workflow (for example, <c>"3"</c>, <c>"3.1.0"</c>, or
    /// <c>"2026-01-29"</c>. When omitted, the strategy derives the version from the type name.
    /// </summary>
    public string? Version { get; init; }
    
    /// <summary>
    /// Gets or sets an optional strategy type to use for this workflow, overriding the globally configured strategy.
    /// The type must implement <see cref="IWorkflowVersionStrategy"/> and expose a public parameterless constructor.
    /// </summary>
    public Type? StrategyType { get; init; }
}
