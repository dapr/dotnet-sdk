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
/// Fluent builder for <see cref="StartWorkflowOptions"/>.
/// </summary>
public sealed class StartWorkflowOptionsBuilder
{
    private string? _instanceId;
    private DateTimeOffset? _startAt;

    /// <summary>
    /// Sets the instance ID for the workflow.
    /// </summary>
    public StartWorkflowOptionsBuilder WithInstanceId(string instanceId)
    {
        _instanceId = instanceId;
        return this;
    }

    /// <summary>
    /// Schedules the workflow to start at a specific date and time.
    /// </summary>
    public StartWorkflowOptionsBuilder StartAt(DateTimeOffset startAt)
    {
        _startAt = startAt;
        return this;
    }

    /// <summary>
    /// Schedules the workflow to start after a delay.
    /// </summary>
    public StartWorkflowOptionsBuilder StartAfter(TimeSpan delay)
    {
        _startAt = DateTimeOffset.UtcNow.Add(delay);
        return this;
    }

    /// <summary>
    /// Builds the <see cref="StartWorkflowOptions"/>.
    /// </summary>
    public StartWorkflowOptions Build() => new StartWorkflowOptions() { InstanceId = _instanceId, StartAt = _startAt };

    /// <summary>
    /// Implicit conversion to <see cref="StartWorkflowOptions"/>.
    /// </summary>
    public static implicit operator StartWorkflowOptions(StartWorkflowOptionsBuilder builder) => builder.Build();
}
