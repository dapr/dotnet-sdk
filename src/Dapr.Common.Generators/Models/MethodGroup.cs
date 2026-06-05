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

using System.Collections.Generic;

namespace Dapr.Common.Generators.Models;

/// <summary>
/// A group of gRPC method variants that share a base name, ordered from
/// most-recent (highest maturity) to oldest.
/// </summary>
internal sealed class MethodGroup
{
    /// <summary>Shared base name, e.g. "ScheduleJob" or "Converse".</summary>
    public string BaseName { get; init; } = string.Empty;

    /// <summary>
    /// The highest-maturity variant: Stable if one exists, otherwise the
    /// most-recent pre-stable variant. This is what the interface exposes.
    /// </summary>
    public MethodVariant MostRecent { get; init; } = null!;

    /// <summary>
    /// Variants to fall back to, in descending maturity order
    /// (2nd-most-recent first, oldest last).
    /// </summary>
    public IReadOnlyList<MethodVariant> Fallbacks { get; init; } = [];

    /// <summary>How the generator handles this group.</summary>
    public MethodClassification Classification { get; init; }
}
