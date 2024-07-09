// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

using Google.Protobuf;

namespace Dapr.Jobs.Models.Responses;

/// <summary>
/// Represents the details of a retrieved job.
/// </summary>
/// <typeparam name="T">The type to deserialize the payload to.</typeparam>
public record JobDetails<T> where T : IMessage
{
    ///<summary>
    /// A cron-like expression that defines when a job should be triggered.
    /// </summary>
    /// <remarks>
    /// Either this or the <see cref="Interval"/> property should be specified.
    /// </remarks>
    public string? CronExpression { get; init; }

    /// <summary>
    /// The interval expression that defines when a job should be triggered.
    /// </summary>
    /// <remarks>
    /// Either this or the <see cref="CronExpression"/> property should be specified.
    /// </remarks>
    public TimeSpan? Interval { get; init; }

    /// <summary>
    /// Allows for jobs with fixed repeat counts.
    /// </summary>
    public uint? RepeatCount { get; init; }

    /// <summary>
    /// Identifies a point-in-time representing when the job schedule should start from,
    /// or as a "one-shot" time if other scheduling fields are not provided.
    /// </summary>
    public DateTime? DueTime { get; init; }

    /// <summary>
    /// A point-in-time value representing with the job should expire.
    /// </summary>
    /// <remarks>
    /// This must be greater than <see cref="DueTime"/> if both are set.
    /// </remarks>
    public DateTime? TTL { get; init; }

    /// <summary>
    /// Stores the main payload of the job which is passed to the trigger function.
    /// </summary>
    public T? Payload { get; init; }
}
