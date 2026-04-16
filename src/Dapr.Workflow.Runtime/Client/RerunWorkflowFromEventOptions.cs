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

namespace Dapr.Workflow.Client;

/// <summary>
/// Options for the <see cref="Dapr.Workflow.IDaprWorkflowClient.RerunWorkflowFromEventAsync"/> operation.
/// </summary>
public sealed class RerunWorkflowFromEventOptions
{
    /// <summary>
    /// Gets or sets the new instance ID to use for the rerun workflow instance.
    /// If not specified, a random instance ID will be generated.
    /// </summary>
    public string? NewInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the optional input to provide when rerunning the workflow, applied at the
    /// next activity event. When set, <see cref="OverwriteInput"/> must also be set to <c>true</c>.
    /// </summary>
    public object? Input { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the workflow's input at the rerun point (for the
    /// next activity event) should be overwritten with <see cref="Input"/>.
    /// </summary>
    public bool OverwriteInput { get; set; }
}
