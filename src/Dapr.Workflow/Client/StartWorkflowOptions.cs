// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

using System;

namespace Dapr.Workflow.Client;

/// <summary>
/// Options for starting a new workflow instance.
/// </summary>
public sealed class StartWorkflowOptions
{
    /// <summary>
    /// Gets or sets the instance ID for the workflow.
    /// </summary>
    /// <remarks>
    /// If not specified, a random GUID will be generated.
    /// </remarks>
    public string? InstanceId { get; set; }
    
    /// <summary>
    /// Gets or sets the scheduled start time for the workflow.
    /// </summary>
    /// <remarks>
    /// If not specified or if a time in the past is specified, the workflow will start immediately. Setting
    /// this alue improves throughput when creating many workflows.
    /// </remarks>
    public DateTimeOffset? StartAt { get; set; }
    
    /// <summary>
    /// Gets or sets the optional identifier of the app on which the workflow should be run.
    /// </summary>
    public string? AppId { get; set; }
}
