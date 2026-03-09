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
/// The result of deriving a canonical name and version from a workflow type.
/// </summary>
/// <param name="CanonicalName">The canonical name for the workflow family.</param>
/// <param name="Version">The derived or explicit version string.</param>
/// <param name="IsExplicit"><see langword="true"/> if provided explicitly (e.g. by <see cref="WorkflowVersionAttribute"/>); otherwise <see langword="false"/>.</param>
public readonly record struct VersionParseResult(string CanonicalName, string Version, bool IsExplicit);
