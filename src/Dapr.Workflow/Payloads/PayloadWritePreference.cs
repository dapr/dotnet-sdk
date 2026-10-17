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

namespace Dapr.Workflow.Payloads;

/// <summary>
/// Controls conversion of newly written, top-level workflow payloads. Reads and retained history
/// are unaffected. Cross-app inputs require inline data unless the call explicitly allows references.
/// </summary>
public enum PayloadWritePreference
{
    /// <summary>
    /// Performs no automatic conversion: ordinary values stay inline and explicit references remain
    /// references. A reference read as an ordinary value and subsequently returned is a new inline value.
    /// This does not preserve an instance-wide storage policy.
    /// </summary>
    Preserve = 0,

    /// <summary>
    /// Automatically stores ordinary values at enabled offload sites when they meet the configured
    /// threshold. Existing references remain references.
    /// </summary>
    PreferReference = 1,

    /// <summary>
    /// Keeps ordinary values inline and resolves references to their original serialized content.
    /// Resolution failures and buffering-limit violations are errors, not fallbacks to references.
    /// The receiving contract must accept the ordinary value rather than a reference.
    /// </summary>
    PreferInline = 2
}
