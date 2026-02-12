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
/// Options for <see cref="ExplicitVersionStrategy"/>.
/// </summary>
public sealed class ExplicitVersionStrategyOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether parsing should be allowed when no explicit version is supplied.
    /// </summary>
    public bool AllowMissingVersion { get; set; }

    /// <summary>
    /// Gets or sets the default version used when no explicit version is supplied and parsing is allowed.
    /// </summary>
    public string DefaultVersion { get; set; } = "0";

    /// <summary>
    /// Gets or sets a value indicating whether version comparisons ignore case.
    /// </summary>
    public bool IgnoreCase { get; set; }
}
