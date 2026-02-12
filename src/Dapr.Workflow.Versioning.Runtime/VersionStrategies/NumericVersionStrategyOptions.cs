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
/// Options for <see cref="NumericVersionStrategy"/>.
/// </summary>
public sealed class NumericVersionStrategyOptions
{
    /// <summary>
    /// Gets or sets the prefix used before the numeric suffix (for example, <c>"V"</c> in <c>MyWorkflowV1</c>).
    /// Set to an empty string to allow a raw numeric suffix (for example, <c>MyWorkflow1</c>).
    /// </summary>
    public string SuffixPrefix { get; set; } = "V";

    /// <summary>
    /// Gets or sets a value indicating whether prefix matching ignores case.
    /// </summary>
    public bool IgnorePrefixCase { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether names without a numeric suffix are allowed.
    /// When enabled, the default version is applied.
    /// </summary>
    public bool AllowNoSuffix { get; set; } = true;

    /// <summary>
    /// Gets or sets the default version used when no suffix is present.
    /// </summary>
    public string DefaultVersion { get; set; } = "0";
}
