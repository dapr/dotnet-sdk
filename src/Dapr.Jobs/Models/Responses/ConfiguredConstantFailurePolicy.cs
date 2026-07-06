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
//  ------------------------------------------------------------------------

namespace Dapr.Jobs.Models.Responses;

/// <summary>
/// Reflects the currently configured policy and values communicated by the Dapr runtime.
/// </summary>
/// <param name="HasMaxRetries">Whether the maximum retries specified by the policy have been reached.</param>
/// <param name="MaxRetries">The maximum number of retries allowed by the configured policy.</param>
/// <param name="Duration">The duration of the retry interval.</param>
public sealed record ConfiguredConstantFailurePolicy(
    bool HasMaxRetries,
    int? MaxRetries,
    TimeSpan Duration) : IFailurePolicyResponse
{
    /// <inheritdoc />
    public JobFailurePolicy Type => JobFailurePolicy.Constant;
}
