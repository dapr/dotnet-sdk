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
/// Options for <see cref="ZeroPaddedNumericVersionStrategy"/>.
/// </summary>
public sealed class ZeroPaddedNumericVersionStrategyOptions
{
    /// <summary>
    /// Gets or sets the prefix used before the numeric suffix (for example, <c>"V"</c> in <c>MyWorkflowV0001</c>).
    /// Set to an empty string to allow a raw numeric suffix.
    /// </summary>
    public string SuffixPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether prefix matching ignores case.
    /// </summary>
    public bool IgnorePrefixCase { get; set; }

    /// <summary>
    /// Gets or sets the required width for the numeric suffix. Set to 0 to allow any width.
    /// </summary>
    public int Width { get; set; } = 4;

    /// <summary>
    /// Gets or sets a value indicating whether names without a numeric suffix are allowed.
    /// When enabled, the default version is applied.
    /// </summary>
    public bool AllowNoSuffix { get; set; }

    /// <summary>
    /// Gets or sets the default version used when no suffix is present.
    /// </summary>
    public string DefaultVersion { get; set; } = "0";
}
