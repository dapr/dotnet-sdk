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
/// Options for <see cref="DelimitedSuffixVersionStrategy"/>.
/// </summary>
public sealed class DelimitedSuffixVersionStrategyOptions
{
    /// <summary>
    /// Gets or sets the delimiter separating the canonical name from the version.
    /// </summary>
    public string Delimiter { get; set; } = "-";

    /// <summary>
    /// Gets or sets a value indicating whether delimiter matching ignores case.
    /// </summary>
    public bool IgnoreDelimiterCase { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether names without a delimiter are allowed.
    /// When enabled, the default version is applied.
    /// </summary>
    public bool AllowNoSuffix { get; set; }

    /// <summary>
    /// Gets or sets the default version used when no suffix is present.
    /// </summary>
    public string DefaultVersion { get; set; } = "0";
}
