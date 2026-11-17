/*
 * Copyright 2026 The Dapr Authors
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *     http://www.apache.org/licenses/LICENSE-2.0
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
limitations under the License.
*/

using System;
using System.Collections.Generic;

namespace Dapr.Workflow.Client;

/// <summary>
/// A workflow schedule, which starts a workflow repeatedly. Each run is a new workflow instance with the ID
/// <c>"&lt;name&gt;_&lt;owner&gt;_&lt;fire time&gt;"</c>, where the owner is a 6-character tag of the app that owns
/// the schedule and the fire time is formatted as <c>yyyyMMddTHHmmssZ</c> (UTC). A run started by
/// <see cref="Dapr.Workflow.IDaprWorkflowClient.TriggerWorkflowScheduleAsync"/> has the ID
/// <c>"&lt;name&gt;_&lt;owner&gt;_&lt;trigger time&gt;_manual"</c>, with the trigger time in milliseconds. So the
/// runs of a schedule are the instances whose ID starts with <c>"&lt;name&gt;_&lt;owner&gt;_"</c>.
/// </summary>
/// <param name="Name">The name of the schedule.</param>
/// <param name="WorkflowName">The name of the workflow each run starts.</param>
/// <param name="Schedule">When runs start, in the syntax of the Dapr Jobs API.</param>
public sealed record WorkflowSchedule(string Name, string WorkflowName, string Schedule)
{
    /// <summary>
    /// Gets the serialized input given to every run, if any.
    /// </summary>
    public string? SerializedInput { get; init; }

    /// <summary>
    /// Gets the time of the first run, if one was set.
    /// </summary>
    public DateTimeOffset? DueTime { get; init; }

    /// <summary>
    /// Gets the time after which no run starts, if one was set.
    /// </summary>
    public DateTimeOffset? ExpirationTime { get; init; }

    /// <summary>
    /// Gets the number of runs after which the schedule completes, if set.
    /// </summary>
    public int? Repeats { get; init; }

    /// <summary>
    /// Gets the app ID that hosts the workflow each run starts, if set.
    /// </summary>
    public string? TargetAppId { get; init; }

    /// <summary>
    /// Gets the namespace of <see cref="TargetAppId"/>, if set.
    /// </summary>
    public string? TargetAppNamespace { get; init; }

    /// <summary>
    /// Gets whether the schedule starts one run for the times it missed while the app was down. When <c>false</c>,
    /// every missed time starts its own run.
    /// </summary>
    public bool SkipMissed { get; init; }

    /// <summary>
    /// Gets how the schedule stands.
    /// </summary>
    public WorkflowScheduleStatus? Status { get; init; }
}

/// <summary>
/// The state of a workflow schedule. The values match the runtime's <c>WorkflowScheduleState</c>.
/// </summary>
public enum WorkflowScheduleState
{
    /// <summary>
    /// The schedule is waiting for its next run.
    /// </summary>
    Active = 0,

    /// <summary>
    /// The schedule has no runs left, because of its repeats or expiration time.
    /// </summary>
    Completed = 1,

    /// <summary>
    /// The schedule stopped with an error, given in <see cref="WorkflowScheduleStatus.FailureMessage"/>.
    /// </summary>
    Failed = 2
}

/// <summary>
/// How a workflow schedule stands.
/// </summary>
/// <param name="State">The state of the schedule.</param>
/// <param name="CreatedAt">When the schedule was created.</param>
/// <param name="RunCount">The number of runs started so far.</param>
public sealed record WorkflowScheduleStatus(WorkflowScheduleState State, DateTimeOffset CreatedAt, int RunCount)
{
    /// <summary>
    /// Gets when the next run starts, unless it is triggered sooner. <c>null</c> once the schedule is not active.
    /// </summary>
    public DateTimeOffset? NextRunTime { get; init; }

    /// <summary>
    /// Gets the time the last run was started for: its fire time, or when it was triggered.
    /// </summary>
    public DateTimeOffset? LastRunTime { get; init; }

    /// <summary>
    /// Gets how the last run stands. <c>null</c> before the first run.
    /// </summary>
    public WorkflowScheduleRun? LastRun { get; init; }

    /// <summary>
    /// Gets why the schedule failed, when <see cref="State"/> is <see cref="WorkflowScheduleState.Failed"/>.
    /// </summary>
    public string? FailureMessage { get; init; }
}

/// <summary>
/// One run of a workflow schedule. Only <see cref="InstanceId"/> is set when the run's workflow instance cannot
/// be read, for example because it was purged.
/// </summary>
/// <param name="InstanceId">The instance ID of the run.</param>
public sealed record WorkflowScheduleRun(string InstanceId)
{
    /// <summary>
    /// Gets the runtime status of the run.
    /// </summary>
    public WorkflowRuntimeStatus? RuntimeStatus { get; init; }

    /// <summary>
    /// Gets when the run was created.
    /// </summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>
    /// Gets when the run was last updated.
    /// </summary>
    public DateTimeOffset? LastUpdatedAt { get; init; }

    /// <summary>
    /// Gets the serialized output of the run, if any.
    /// </summary>
    public string? SerializedOutput { get; init; }

    /// <summary>
    /// Gets the failure details, if the run failed.
    /// </summary>
    public WorkflowTaskFailureDetails? FailureDetails { get; init; }
}

/// <summary>
/// Represents a page of workflow schedules returned by a list operation.
/// </summary>
/// <param name="Schedules">The workflow schedules in this page.</param>
/// <param name="ContinuationToken">
/// The continuation token used to retrieve the next page, or <c>null</c> if there are no more pages.
/// </param>
public sealed record WorkflowSchedulePage(
    IReadOnlyList<WorkflowSchedule> Schedules,
    string? ContinuationToken);
