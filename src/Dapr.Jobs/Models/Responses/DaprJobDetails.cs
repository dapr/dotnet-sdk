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

using System.Text.Json.Serialization;
using Dapr.Jobs.JsonConverters;

namespace Dapr.Jobs.Models.Responses;

/// <summary>
/// Represents the details of a retrieved job.
/// </summary>
/// <param name="Schedule">The job schedule.</param>
public sealed record DaprJobDetails(DaprJobSchedule Schedule)
{
    /// <summary>
    /// Allows for jobs with fixed repeat counts.
    /// </summary>
    public int? RepeatCount { get; init; } = null;

    /// <summary>
    /// Identifies a point-in-time representing when the job schedule should start from,
    /// or as a "one-shot" time if other scheduling fields are not provided.
    /// </summary>
    public DateTimeOffset? DueTime { get; init; } = null;

    /// <summary>
    /// A point-in-time value representing with the job should expire.
    /// </summary>
    /// <remarks>
    /// This must be greater than <see cref="DueTime"/> if both are set.
    /// </remarks>
    public DateTimeOffset? Ttl { get; init; } = null;

    /// <summary>
    /// Stores the main payload of the job which is passed to the trigger function.
    /// </summary>
    public byte[]? Payload { get; init; } = null;
}

/// <summary>
/// A deserializable version of the <see cref="DaprJobDetails"/>.
/// </summary>
internal sealed record DeserializableDaprJobDetails
{
    /// <summary>
    /// Represents the schedule that triggers the job.
    /// </summary>
    public string? Schedule { get; init; }

    /// <summary>
    /// Allows for jobs with fixed repeat counts.
    /// </summary>
    public int? RepeatCount { get; init; } = null;

    /// <summary>
    /// Identifies a point-in-time representing when the job schedule should start from,
    /// or as a "one-shot" time if other scheduling fields are not provided.
    /// </summary>
    [JsonConverter(typeof(Iso8601DateTimeJsonConverter))]
    public DateTimeOffset? DueTime { get; init; } = null;

    /// <summary>
    /// A point-in-time value representing with the job should expire.
    /// </summary>
    /// <remarks>
    /// This must be greater than <see cref="DueTime"/> if both are set.
    /// </remarks>
    [JsonConverter(typeof(Iso8601DateTimeJsonConverter))]
    public DateTimeOffset? Ttl { get; init; } = null;

    /// <summary>
    /// Stores the main payload of the job which is passed to the trigger function.
    /// </summary>
    public byte[]? Payload { get; init; } = null;

    public DaprJobDetails ToType()
    {
        var schedule = DaprJobSchedule.FromExpression(Schedule ?? string.Empty);
        return new DaprJobDetails(schedule)
        {
            DueTime = DueTime,
            Payload = Payload,
            RepeatCount = RepeatCount,
            Ttl = Ttl
        };
    }
}
