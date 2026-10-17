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

using System;

namespace Dapr.Workflow.Payloads;

/// <summary>
/// Identifies the boundary at which a workflow payload is being serialized or deserialized.
/// Values are flags so that policies can be expressed as a combination of boundaries.
/// </summary>
[Flags]
public enum PayloadKind
{
    /// <summary>
    /// No boundary. Used as the empty value for policy sets.
    /// </summary>
    None = 0,
 
    /// <summary>
    /// The input supplied when a workflow instance is scheduled.
    /// </summary>
    WorkflowInput = 1 << 0,
 
    /// <summary>
    /// The value returned by a workflow when it completes.
    /// </summary>
    WorkflowOutput = 1 << 1,
 
    /// <summary>
    /// The input passed to an activity when it is invoked.
    /// </summary>
    ActivityInput = 1 << 2,
 
    /// <summary>
    /// The value returned by an activity.
    /// </summary>
    ActivityOutput = 1 << 3,
 
    /// <summary>
    /// The payload of an external event raised to a workflow instance.
    /// </summary>
    ExternalEvent = 1 << 4,
 
    /// <summary>
    /// The input supplied to a workflow when it continues as a new instance.
    /// </summary>
    ContinueAsNewInput = 1 << 5,
 
    /// <summary>
    /// The custom status value set by a workflow.
    /// </summary>
    CustomStatus = 1 << 6,
 
    /// <summary>
    /// The input passed to a child workflow when it is scheduled.
    /// </summary>
    ChildWorkflowInput = 1 << 7,
 
    /// <summary>
    /// The value returned by a child workflow to its parent.
    /// </summary>
    ChildWorkflowOutput = 1 << 8,
 
    /// <summary>
    /// All boundaries.
    /// </summary>
    All = WorkflowInput | WorkflowOutput | ActivityInput | ActivityOutput | ExternalEvent
          | ContinueAsNewInput | CustomStatus | ChildWorkflowInput | ChildWorkflowOutput,
}
