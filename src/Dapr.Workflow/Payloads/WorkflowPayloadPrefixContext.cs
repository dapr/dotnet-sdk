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

/// <summary>Immutable metadata available when choosing the prefix for a new payload.</summary>
/// <remarks>
/// Names are registered workflow/activity names, not inferred CLR type names.
/// Missing names remain null; the SDK does not fetch metadata to fill them.
/// Prefixes organize storage and are not an authorization boundary.
/// </remarks>
public sealed record WorkflowPayloadPrefixContext
{
    /// <summary>Creates metadata for an explicit payload write.</summary>
    /// <param name="instanceId">The workflow instance that owns the payload.</param>
    /// <param name="workflowName">The owning workflow's registered name, when known.</param>
    /// <param name="activityName">The associated activity's registered name, when applicable and known.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="instanceId"/> is blank, or a supplied name is empty or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="instanceId"/> is null.</exception>
    public WorkflowPayloadPrefixContext(string instanceId, string? workflowName = null, string? activityName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        if (workflowName is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(workflowName);
        if (activityName is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(activityName);
        InstanceId = instanceId;
        WorkflowName = workflowName;
        ActivityName = activityName;
    }

    /// <summary>Gets the instance that owns the payload. The SDK appends this as an escaped path segment.</summary>
    public string InstanceId { get; }

    /// <summary>
    /// Gets the owning workflow's registered name, or null when unavailable.
    /// In an activity this requires matching propagated workflow history from the runtime.
    /// </summary>
    /// <remarks>
    /// Scheduling and workflow-worker writes supply the name when known. Explicit resolver callers may
    /// supply it themselves. Activity-context extensions receive it internally, not from a public activity property.
    /// </remarks>
    public string? WorkflowName { get; }

    /// <summary>
    /// Gets the registered activity name for activity inputs, outputs, and explicit activity-context writes;
    /// otherwise null unless supplied by an explicit caller.
    /// </summary>
    public string? ActivityName { get; }
}
