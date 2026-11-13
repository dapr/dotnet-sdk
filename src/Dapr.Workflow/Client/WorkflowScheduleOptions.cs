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

namespace Dapr.Workflow.Client;

/// <summary>
/// Options for the <see cref="Dapr.Workflow.IDaprWorkflowClient.CreateWorkflowScheduleAsync"/> operation.
/// </summary>
public sealed class WorkflowScheduleOptions
{
    /// <summary>
    /// Gets or sets the input given to every run. It is serialized with the configured workflow serializer.
    /// </summary>
    public object? Input { get; set; }

    /// <summary>
    /// Gets or sets the time of the first run. If not set, the first run is at the first time the
    /// schedule matches after it is created.
    /// </summary>
    public DateTimeOffset? DueTime { get; set; }

    /// <summary>
    /// Gets or sets the time after which no run starts. The schedule completes once its next run would be
    /// after it.
    /// </summary>
    public DateTimeOffset? ExpirationTime { get; set; }

    /// <summary>
    /// Gets or sets the number of runs after which the schedule completes. Must be at least 1.
    /// </summary>
    public int? Repeats { get; set; }

    /// <summary>
    /// Gets or sets the app ID that hosts the workflow each run starts. Defaults to the app that owns the schedule.
    /// </summary>
    public string? TargetAppId { get; set; }

    /// <summary>
    /// Gets or sets the namespace of <see cref="TargetAppId"/>. Requires <see cref="TargetAppId"/>.
    /// </summary>
    public string? TargetAppNamespace { get; set; }

    /// <summary>
    /// Gets or sets whether to start one run for the times the schedule missed while the app was down, then carry
    /// on from the next time after now. By default (<c>false</c>), the schedule catches up durably: every missed
    /// time starts its own run, one after another, as with Dapr jobs and actor reminders.
    /// </summary>
    public bool SkipMissed { get; set; }

    /// <summary>
    /// Gets or sets whether to replace an existing schedule of the same name. Runs it already started are not
    /// affected. Without it, creating a schedule whose name is taken fails with an
    /// <see cref="global::Grpc.Core.RpcException"/> whose status code is <see cref="global::Grpc.Core.StatusCode.AlreadyExists"/>.
    /// </summary>
    public bool Overwrite { get; set; }
}
