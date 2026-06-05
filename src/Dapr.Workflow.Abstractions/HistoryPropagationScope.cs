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

namespace Dapr.Workflow;

/// <summary>
/// Defines the scope of workflow history that is propagated to a child workflow.
/// </summary>
public enum HistoryPropagationScope
{
    /// <summary>
    /// No history is propagated to child workflows. This is the default behavior.
    /// </summary>
    None = 0,

    /// <summary>
    /// Only the calling workflow's own history events are propagated to the child.
    /// Ancestor history is excluded, acting as a trust boundary.
    /// </summary>
    OwnHistory = 1,

    /// <summary>
    /// The calling workflow's history and all ancestor history (the full lineage) is propagated.
    /// </summary>
    Lineage = 2,
}
