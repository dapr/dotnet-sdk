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

namespace Dapr.Search;

/// <summary>Selects and shapes a page of search hits. Field names address document content.</summary>
public sealed record SearchQuery
{
    /// <summary>Lexical query text. Mutually exclusive with Native. Neither means match all.</summary>
    public string? Text { get; init; }

    /// <summary>Provider-native query as a JSON object. Mutually exclusive with Text.</summary>
    public JsonElement? Native { get; init; }

    /// <summary>
    /// Portable filter JSON object supporting $eq, $ne, $gt, $gte, $lt, $lte, $in, $nin,
    /// $exists, $and, $or and $not. Nested field paths use dotted notation.
    /// </summary>
    /// <remarks>Use <see cref="SearchFilter"/> to compose filters without writing JSON manually.</remarks>
    public JsonElement? Filter { get; init; }

    /// <summary>Maximum hits in this page. Null or zero uses the provider default.</summary>
    public uint? TopK { get; init; }

    /// <summary>Opaque token returned by the preceding page. Null starts a new query.</summary>
    public string? ContinuationToken { get; init; }

    /// <summary>Fields projected into the returned content.</summary>
    public IReadOnlyList<string> ReturnFields { get; init; } = [];

    /// <summary>Whether to return content. Null and false omit it.</summary>
    public bool? IncludeContent { get; init; }

    /// <summary>Fields searched for lexical matches.</summary>
    public IReadOnlyList<string> SearchFields { get; init; } = [];

    /// <summary>Ordered sort clauses.</summary>
    public IReadOnlyList<SortClause> Sort { get; init; } = [];

    /// <summary>Fields for which highlighted fragments are requested.</summary>
    public IReadOnlyList<string> HighlightFields { get; init; } = [];
}

/// <summary>Sort direction.</summary>
public enum SortOrder
{
    /// <summary>Provider-default direction.</summary>
    Unspecified = 0,

    /// <summary>Ascending order.</summary>
    Ascending = 1,

    /// <summary>Descending order.</summary>
    Descending = 2
}

/// <summary>Sorts hits by a content field.</summary>
/// <param name="Field">Top-level or dotted nested field path.</param>
/// <param name="Order">Sort direction.</param>
public sealed record SortClause(string Field, SortOrder Order = SortOrder.Ascending);
