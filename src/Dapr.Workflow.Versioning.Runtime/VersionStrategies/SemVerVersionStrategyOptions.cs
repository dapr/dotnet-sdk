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
/// Options for <see cref="SemVerVersionStrategy"/>.
/// </summary>
public sealed class SemVerVersionStrategyOptions
{
    /// <summary>
    /// Gets or sets the prefix expected before the SemVer suffix (for example, <c>"v"</c> in <c>MyWorkflowv1.2.3</c>).
    /// Set to an empty string to require no prefix.
    /// </summary>
    public string Prefix { get; set; } = "v";

    /// <summary>
    /// Gets or sets a value indicating whether prefix matching ignores case.
    /// </summary>
    public bool IgnorePrefixCase { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether pre-release labels (for example, <c>-alpha.1</c>) are allowed.
    /// </summary>
    public bool AllowPrerelease { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether build metadata (for example, <c>+build.5</c>) is allowed.
    /// </summary>
    public bool AllowBuildMetadata { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether names without a SemVer suffix are allowed.
    /// When enabled, the default version is applied.
    /// </summary>
    public bool AllowNoSuffix { get; set; }

    /// <summary>
    /// Gets or sets the default version used when no suffix is present.
    /// </summary>
    public string DefaultVersion { get; set; } = "0.0.0";
}
