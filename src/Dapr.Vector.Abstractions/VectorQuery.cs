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

using System.Text.Json;

namespace Dapr.Vector;

/// <summary>Selects and shapes one dense-vector query. Exactly one of Vector and ById is required.</summary>
public sealed record VectorQuery
{
    /// <summary>Non-empty pre-computed query embedding, mutually exclusive with ById.</summary>
    public IReadOnlyList<float>? Vector { get; init; }

    /// <summary>Identifier of a stored vector to query with, mutually exclusive with Vector.</summary>
    public string? ById { get; init; }

    /// <summary>Maximum matches. Null or zero selects the component default.</summary>
    public uint? TopK { get; init; }

    /// <summary>Portable filter DSL JSON object addressing record metadata.</summary>
    public JsonElement? Filter { get; init; }

    /// <summary>Return embedding values; omitted defaults to false.</summary>
    public bool? IncludeValues { get; init; }

    /// <summary>Return payloads; omitted defaults to false.</summary>
    public bool? IncludePayload { get; init; }

    /// <summary>Metric to evaluate, or Unspecified to use the collection metric.</summary>
    public DistanceMetric Metric { get; init; }

    /// <summary>
    /// Inclusive cutoff: scores at or above this for cosine/dot product, at or below this for Euclidean.
    /// Scores are not normalized.
    /// </summary>
    public double? ScoreThreshold { get; init; }
}
