// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

namespace Dapr.Jobs.Models;

/// <summary>
/// A policy which retries the job at a consistent interval when the job
/// fails to trigger.
/// </summary>
/// <param name="Interval">The constant delay to wait before trying the job.</param>
public sealed record JobFailurePolicyConstantOptions(TimeSpan Interval) : IJobFailurePolicyOptions
{
    /// <summary>
    /// An optional maximum number of retries to attempt before giving up. If unset,
    /// the Job will be retried indefinitely.
    /// </summary>
    public uint? MaxRetries { get; init; } = null;

    /// <summary>
    /// The type of policy to apply.
    /// </summary>
    public JobFailurePolicy Policy => JobFailurePolicy.Constant;
}
