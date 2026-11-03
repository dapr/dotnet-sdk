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

using System.Buffers;
using System.Text.Json;

namespace Dapr.Search;

/// <summary>Creates immutable, composable portable search filters.</summary>
/// <remarks>
/// Field names are serialized document paths, including dotted nested paths; they are not renamed
/// by serializer options. Values are serialized when a filter is created. Supply the same value
/// serialization options used to index documents when using custom converters.
/// Provider-specific restrictions on fields and values are enforced by the search component.
/// </remarks>
public sealed class SearchFilter
{
    private readonly JsonElement filter;

    private SearchFilter(JsonElement filter) => this.filter = filter;

    /// <summary>Matches a field equal to the supplied value.</summary>
    public static SearchFilter EqualTo(string field, object? value, JsonSerializerOptions? options = null) =>
        Compare(field, "$eq", value, options);

    /// <summary>Matches a field not equal to the supplied value.</summary>
    public static SearchFilter NotEqualTo(string field, object? value, JsonSerializerOptions? options = null) =>
        Compare(field, "$ne", value, options);

    /// <summary>Matches a field greater than the supplied value.</summary>
    public static SearchFilter GreaterThan(string field, object? value, JsonSerializerOptions? options = null) =>
        Compare(field, "$gt", value, options);

    /// <summary>Matches a field greater than or equal to the supplied value.</summary>
    public static SearchFilter GreaterThanOrEqualTo(string field, object? value, JsonSerializerOptions? options = null) =>
        Compare(field, "$gte", value, options);

    /// <summary>Matches a field less than the supplied value.</summary>
    public static SearchFilter LessThan(string field, object? value, JsonSerializerOptions? options = null) =>
        Compare(field, "$lt", value, options);

    /// <summary>Matches a field less than or equal to the supplied value.</summary>
    public static SearchFilter LessThanOrEqualTo(string field, object? value, JsonSerializerOptions? options = null) =>
        Compare(field, "$lte", value, options);

    /// <summary>Matches a field equal to any of the supplied values.</summary>
    public static SearchFilter In<T>(string field, IEnumerable<T> values, JsonSerializerOptions? options = null) =>
        Membership(field, "$in", values, options);

    /// <summary>Matches a field not equal to any of the supplied values.</summary>
    public static SearchFilter NotIn<T>(string field, IEnumerable<T> values, JsonSerializerOptions? options = null) =>
        Membership(field, "$nin", values, options);

    /// <summary>Matches a field's presence, or absence when <paramref name="exists"/> is false.</summary>
    public static SearchFilter Exists(string field, bool exists = true) =>
        Field(field, "$exists", writer => writer.WriteBooleanValue(exists));

    /// <summary>Matches all of the supplied filters. At least one filter is required.</summary>
    public static SearchFilter And(params SearchFilter[] filters) => Combine("$and", filters);

    /// <summary>Matches any of the supplied filters. At least one filter is required.</summary>
    public static SearchFilter Or(params SearchFilter[] filters) => Combine("$or", filters);

    /// <summary>Negates the supplied filter.</summary>
    public static SearchFilter Not(SearchFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return Create(writer =>
        {
            writer.WritePropertyName("$not");
            filter.filter.WriteTo(writer);
        });
    }

    /// <summary>Copies a raw filter JSON object for composition with other filters.</summary>
    /// <remarks>Validates only that the value is an object, not its operators or provider-specific semantics.</remarks>
    public static SearchFilter FromJson(JsonElement filter)
    {
        if (filter.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Expected a JSON object.", nameof(filter));
        return new SearchFilter(filter.Clone());
    }

    /// <summary>Returns the filter JSON object for <see cref="SearchQuery.Filter"/>.</summary>
    /// <remarks>The result owns its JSON data and remains valid independently of source documents and collections.</remarks>
    public JsonElement Build() => filter;

    private static SearchFilter Compare(string field, string operation, object? value,
        JsonSerializerOptions? options) =>
        Field(field, operation, writer => JsonSerializer.Serialize(writer, value, options));

    private static SearchFilter Membership<T>(
        string field, string operation, IEnumerable<T> values, JsonSerializerOptions? options)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Field(field, operation, writer =>
        {
            writer.WriteStartArray();
            foreach (var value in values)
                JsonSerializer.Serialize(writer, value, options);
            writer.WriteEndArray();
        });
    }

    private static SearchFilter Field(string field, string operation, Action<Utf8JsonWriter> writeValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        return Create(writer =>
        {
            writer.WriteStartObject(field);
            writer.WritePropertyName(operation);
            writeValue(writer);
            writer.WriteEndObject();
        });
    }

    private static SearchFilter Combine(string operation, SearchFilter[] filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        if (filters.Length == 0)
            throw new ArgumentException("At least one filter is required.", nameof(filters));
        return Create(writer =>
        {
            writer.WriteStartArray(operation);
            foreach (var filter in filters)
            {
                if (filter is null)
                    throw new ArgumentException("Filters must not contain null entries.", nameof(filters));
                filter.filter.WriteTo(writer);
            }

            writer.WriteEndArray();
        });
    }

    private static SearchFilter Create(Action<Utf8JsonWriter> writeProperties)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writeProperties(writer);
            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return new SearchFilter(document.RootElement.Clone());
    }
}
