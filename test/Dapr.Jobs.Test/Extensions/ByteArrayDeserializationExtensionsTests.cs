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

#nullable enable

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapr.Jobs.Extensions;
using Xunit;

namespace Dapr.Jobs.Test.Extensions;

public class ByteArrayDeserializationExtensionsTests
{
    [Fact]
    public void DeserializeToString_Deserialize()
    {
        const string originalStringValue = "This is a simple test!";
        var serializedString = Encoding.UTF8.GetBytes(originalStringValue);

        var deserializedString = ByteArrayDeserializationExtensions.DeserializeToString(serializedString);
        Assert.Equal(originalStringValue, deserializedString);
    }

    [Fact]
    public void DeserializeToJsonObject_Deserialize()
    {
        var originalType = new TestType { Name = "Test", Value = 5 };
        var serialized = JsonSerializer.SerializeToUtf8Bytes(originalType);

        var deserializedType = ByteArrayDeserializationExtensions.DeserializeFromJsonBytes<TestType>(serialized);
        Assert.Equal(originalType, deserializedType);
    }

    [Fact]
    public void DeserializeToJsonObject_DeserializeWithOptions()
    {
        const string json = "{\"value\": \"15\"}";
        var jsonOptions = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, PropertyNameCaseInsensitive = true, NumberHandling = JsonNumberHandling.AllowReadingFromString};
        var serializedBytes = Encoding.UTF8.GetBytes(json);

        var deserializedType = ByteArrayDeserializationExtensions.DeserializeFromJsonBytes<TestType>(serializedBytes, jsonOptions);
        Assert.NotNull(deserializedType);
        Assert.Null(deserializedType.Name);
        Assert.Equal(15, deserializedType.Value);
    }

    private sealed record TestType
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; } = null;

        [JsonPropertyName("value")]
        public int Value { get; init; }
    }
}
