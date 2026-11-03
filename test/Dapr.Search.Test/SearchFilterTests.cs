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
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Dapr.Search.Test;

public sealed class SearchFilterTests
{
    [Theory]
    [InlineData(nameof(SearchFilter.EqualTo), "$eq")]
    [InlineData(nameof(SearchFilter.NotEqualTo), "$ne")]
    [InlineData(nameof(SearchFilter.GreaterThan), "$gt")]
    [InlineData(nameof(SearchFilter.GreaterThanOrEqualTo), "$gte")]
    [InlineData(nameof(SearchFilter.LessThan), "$lt")]
    [InlineData(nameof(SearchFilter.LessThanOrEqualTo), "$lte")]
    public void ComparisonsUseFieldFirstSyntax(string method, string operation)
    {
        var filter = Comparison(method, "product.price", 12.5m).Build();

        var field = Assert.Single(filter.EnumerateObject());
        Assert.Equal("product.price", field.Name);
        var comparison = Assert.Single(field.Value.EnumerateObject());
        Assert.Equal(operation, comparison.Name);
        Assert.Equal(12.5m, comparison.Value.GetDecimal());
    }

    [Theory]
    [InlineData("\"a\\\"b\\\\c\\n\"")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("-12.5")]
    public void ComparisonPreservesJsonValueTypesAndEscaping(string json)
    {
        using var document = JsonDocument.Parse(json);
        var filter = SearchFilter.EqualTo("nested.\"field\\name", document.RootElement);
        var actual = filter.Build().GetProperty("nested.\"field\\name").GetProperty("$eq");
        AssertJson(json, actual);
    }

    [Fact]
    public void ClrValuesAreSerializedWithoutStringCoercion()
    {
        AssertJson("""{"x":{"$eq":null}}""", SearchFilter.EqualTo("x", null).Build());
        AssertJson("""{"x":{"$eq":true}}""", SearchFilter.EqualTo("x", true).Build());
        AssertJson("""{"x":{"$eq":"quoted \"value\""}}""", SearchFilter.EqualTo("x", "quoted \"value\"").Build());
        AssertJson("""{"x":{"$eq":123.45}}""", SearchFilter.EqualTo("x", 123.45m).Build());
    }

    [Theory]
    [InlineData(false, "$in")]
    [InlineData(true, "$nin")]
    public void MembershipPreservesValuesAndOrder(bool negate, string operation)
    {
        object?[] values = ["books", 10, true, null];
        var filter = negate ? SearchFilter.NotIn("category", values) : SearchFilter.In("category", values);
        var field = Assert.Single(filter.Build().EnumerateObject());
        Assert.Equal("category", field.Name);
        var membership = Assert.Single(field.Value.EnumerateObject());
        Assert.Equal(operation, membership.Name);
        AssertJson("""["books",10,true,null]""", membership.Value);
    }

    [Fact]
    public void MembershipPreservesEmptySets()
    {
        AssertJson("""{"x":{"$in":[]}}""", SearchFilter.In("x", Array.Empty<int>()).Build());
        AssertJson("""{"x":{"$nin":[]}}""", SearchFilter.NotIn("x", Array.Empty<int>()).Build());
    }

    [Fact]
    public void ExistsWritesBooleanWithoutValueConverters()
    {
        AssertJson("""{"x":{"$exists":true}}""", SearchFilter.Exists("x").Build());
        AssertJson("""{"x":{"$exists":false}}""", SearchFilter.Exists("x", false).Build());
    }

    [Fact]
    public void LogicalFiltersPreserveNestingAndRepeatedFields()
    {
        var filter = SearchFilter.And(
            SearchFilter.Or(SearchFilter.EqualTo("category", "books"),
                SearchFilter.In("category", new[] { "videos", "games" })),
            SearchFilter.GreaterThanOrEqualTo("price", 10),
            SearchFilter.LessThan("price", 100),
            SearchFilter.Not(SearchFilter.Exists("deleted")));

        AssertJson("""
                   {"$and":[
                       {"$or":[{"category":{"$eq":"books"}},{"category":{"$in":["videos","games"]}}]},
                       {"price":{"$gte":10}},
                       {"price":{"$lt":100}},
                       {"$not":{"deleted":{"$exists":true}}}
                   ]}
                   """, filter.Build());
    }

    [Fact]
    public void SingleOperandGroupsAndNestedNegationRemainExplicit()
    {
        var filter = SearchFilter.EqualTo("x", 1);
        AssertJson("""{"$and":[{"x":{"$eq":1}}]}""", SearchFilter.And(filter).Build());
        AssertJson("""{"$or":[{"x":{"$eq":1}}]}""", SearchFilter.Or(filter).Build());
        AssertJson("""{"$not":{"$not":{"x":{"$eq":1}}}}""", SearchFilter.Not(SearchFilter.Not(filter)).Build());
    }

    [Fact]
    public void FiltersSnapshotCollectionsAndCanBeReused()
    {
        var values = new List<int> { 1, 2 };
        var member = SearchFilter.In("x", values);
        SearchFilter[] children = [member];
        var first = SearchFilter.And(children);
        var built = first.Build();
        values.Add(3);
        children[0] = SearchFilter.EqualTo("x", 4);
        var second = SearchFilter.Or(member, SearchFilter.EqualTo("x", 5));

        AssertJson("""{"$and":[{"x":{"$in":[1,2]}}]}""", built);
        AssertJson(built.GetRawText(), first.Build());
        AssertJson("""{"$or":[{"x":{"$in":[1,2]}},{"x":{"$eq":5}}]}""", second.Build());
    }

    [Fact]
    public void RawFiltersAndJsonOperandsSurviveSourceDocumentDisposal()
    {
        SearchFilter raw;
        SearchFilter comparison;
        SearchFilter membership;
        using (var document = JsonDocument.Parse("""{"x":{"$eq":"books"}}"""))
        {
            raw = SearchFilter.FromJson(document.RootElement);
            var value = document.RootElement.GetProperty("x").GetProperty("$eq");
            comparison = SearchFilter.EqualTo("x", value);
            membership = SearchFilter.In("x", new[] { value });
        }

        AssertJson("""{"x":{"$eq":"books"}}""", raw.Build());
        AssertJson(raw.Build().GetRawText(), comparison.Build());
        AssertJson("""{"x":{"$in":["books"]}}""", membership.Build());
        AssertJson("""{"$not":{"x":{"$eq":"books"}}}""", SearchFilter.Not(raw).Build());
    }

    [Fact]
    public void SerializerOptionsApplyToValuesNotFilterSyntaxOrFieldPaths()
    {
        var options = new JsonSerializerOptions
        {
            DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseUpper,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter<DayOfWeek>(JsonNamingPolicy.CamelCase) }
        };
        var filter = SearchFilter.EqualTo("customer.dayOfWeek", DayOfWeek.Monday, options);
        AssertJson("""{"customer.dayOfWeek":{"$eq":"monday"}}""", filter.Build());
        AssertJson("""{"days":{"$in":["monday","tuesday"]}}""",
            SearchFilter.In("days", new[] { DayOfWeek.Monday, DayOfWeek.Tuesday }, options).Build());
        AssertJson("""{"days":{"$nin":["monday"]}}""",
            SearchFilter.NotIn("days", new[] { DayOfWeek.Monday }, options).Build());
        AssertJson("""{"x":{"$eq":null}}""", SearchFilter.EqualTo("x", null, options).Build());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void FieldOperatorsRejectMissingFields(string? field)
    {
        foreach (var method in new[]
                 {
                     nameof(SearchFilter.EqualTo), nameof(SearchFilter.NotEqualTo),
                     nameof(SearchFilter.GreaterThan), nameof(SearchFilter.GreaterThanOrEqualTo),
                     nameof(SearchFilter.LessThan), nameof(SearchFilter.LessThanOrEqualTo)
                 })
            Assert.ThrowsAny<ArgumentException>(() => Comparison(method, field!, 1));
        Assert.ThrowsAny<ArgumentException>(() => SearchFilter.In(field!, new[] { 1 }));
        Assert.ThrowsAny<ArgumentException>(() => SearchFilter.NotIn(field!, new[] { 1 }));
        Assert.ThrowsAny<ArgumentException>(() => SearchFilter.Exists(field!));
    }

    [Fact]
    public void MembershipRejectsNullCollections()
    {
        Assert.Throws<ArgumentNullException>("values", () => SearchFilter.In<int>("x", null!));
        Assert.Throws<ArgumentNullException>("values", () => SearchFilter.NotIn<int>("x", null!));
    }

    [Fact]
    public void LogicalOperatorsRejectNullAndEmptyOperands()
    {
        Assert.Throws<ArgumentNullException>("filters", () => SearchFilter.And(null!));
        Assert.Throws<ArgumentNullException>("filters", () => SearchFilter.Or(null!));
        Assert.Throws<ArgumentException>("filters", () => SearchFilter.And());
        Assert.Throws<ArgumentException>("filters", () => SearchFilter.Or());
        Assert.Throws<ArgumentException>("filters", () => SearchFilter.And(SearchFilter.EqualTo("x", 1), null!));
        Assert.Throws<ArgumentException>("filters", () => SearchFilter.Or(SearchFilter.EqualTo("x", 1), null!));
        Assert.Throws<ArgumentNullException>("filter", () => SearchFilter.Not(null!));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("\"text\"")]
    public void RawFilterRejectsNonObjects(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Throws<ArgumentException>("filter", () => SearchFilter.FromJson(document.RootElement));
    }

    [Fact]
    public void RawFilterRejectsUndefinedButPreservesUnrecognizedOperators()
    {
        Assert.Throws<ArgumentException>("filter", () => SearchFilter.FromJson(default));
        using var document = JsonDocument.Parse("""{"x":{"$future":42}}""");
        AssertJson(document.RootElement.GetRawText(), SearchFilter.FromJson(document.RootElement).Build());
    }

    [Fact]
    public void SerializationAndEnumerationFailuresPropagate()
    {
        Assert.Throws<ArgumentException>(() => SearchFilter.EqualTo("x", double.NaN));
        Assert.Throws<NotSupportedException>(() => SearchFilter.EqualTo("x", typeof(string)));
        Assert.Throws<InvalidOperationException>(() => SearchFilter.In("x", FailingValues()));
    }

    private static IEnumerable<int> FailingValues()
    {
        yield return 1;
        throw new InvalidOperationException("Failed to enumerate values.");
    }

    private static SearchFilter Comparison(string method, string field, object? value) => method switch
    {
        nameof(SearchFilter.EqualTo) => SearchFilter.EqualTo(field, value),
        nameof(SearchFilter.NotEqualTo) => SearchFilter.NotEqualTo(field, value),
        nameof(SearchFilter.GreaterThan) => SearchFilter.GreaterThan(field, value),
        nameof(SearchFilter.GreaterThanOrEqualTo) => SearchFilter.GreaterThanOrEqualTo(field, value),
        nameof(SearchFilter.LessThan) => SearchFilter.LessThan(field, value),
        nameof(SearchFilter.LessThanOrEqualTo) => SearchFilter.LessThanOrEqualTo(field, value),
        _ => throw new ArgumentOutOfRangeException(nameof(method))
    };

    private static void AssertJson(string expected, JsonElement actual) =>
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual.GetRawText())),
            $"Expected {expected}, received {actual.GetRawText()}.");
}
