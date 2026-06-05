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

namespace Dapr.Common;

/// <summary>
/// Thrown when a Dapr feature is not available on the connected Dapr runtime in any known API version.
/// This exception is distinct from general <see cref="DaprException"/> failures: it specifically indicates
/// that gRPC reflection confirmed none of the checked method variants exist on the runtime, meaning the
/// feature requires a newer Dapr runtime version.
/// </summary>
/// <param name="featureName">The logical name of the feature (e.g., "ListJobs").</param>
/// <param name="checkedVariants">
/// The fully-qualified gRPC method names that were checked and not found
/// (e.g., "dapr.proto.runtime.v1.Dapr/ListJobs", "dapr.proto.runtime.v1.Dapr/ListJobsAlpha1").
/// </param>
public sealed class DaprFeatureNotAvailableException(string featureName, string[] checkedVariants) : DaprException(BuildMessage(featureName, checkedVariants))
{
    /// <summary>
    /// The logical name of the unavailable feature.
    /// </summary>
    public string FeatureName { get; } = featureName;

    /// <summary>
    /// The fully-qualified gRPC method names that were checked against the runtime
    /// and not found to be supported.
    /// </summary>
    public IReadOnlyList<string> CheckedVariants { get; } = checkedVariants;

    private static string BuildMessage(string featureName, string[] checkedVariants)
    {
        var variantList = string.Join(", ", checkedVariants);
        return $"The '{featureName}' feature is not available on the connected Dapr runtime. " +
               $"Checked for: {variantList}. " +
               $"This feature may require a newer Dapr runtime version.";
    }
}
