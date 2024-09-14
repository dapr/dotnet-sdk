using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapr.Jobs.Extensions;
using Xunit;

#nullable enable

namespace Dapr.Jobs.Test;

public class ByteArrayDeserializationExtensionsTest
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
