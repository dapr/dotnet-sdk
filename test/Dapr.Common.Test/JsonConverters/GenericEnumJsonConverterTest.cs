using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapr.Common.JsonConverters;
using Xunit;

namespace Dapr.Common.Test.JsonConverters;

public class GenericEnumJsonConverterTest
{
    [Fact]
    public void ShouldSerializeWithEnumMemberAttribute()
    {
        var testValue = new TestType("ColorTest", Color.Red);
        var serializedValue = JsonSerializer.Serialize(testValue);
        Assert.Equal("{\"Name\":\"ColorTest\",\"Color\":\"definitely-not-red\"}", serializedValue);
    }

    [Fact]
    public void ShouldSerializeWithoutEnumMemberAttribute()
    {
        var testValue = new TestType("ColorTest", Color.Green);
        var serializedValue = JsonSerializer.Serialize(testValue);
        Assert.Equal("{\"Name\":\"ColorTest\",\"Color\":\"Green\"}", serializedValue);
    }

    [Fact]
    public void ShouldDeserializeWithEnumMemberAttribute()
    {
        const string json = "{\"Name\":\"ColorTest\",\"Color\":\"definitely-not-red\"}";
        var deserializedValue = JsonSerializer.Deserialize<TestType>(json);
        Assert.Equal("ColorTest", deserializedValue.Name);
        Assert.Equal(Color.Red, deserializedValue.Color);
    }

    [Fact]
    public void ShouldDeserializeWithoutEnumMemberAttribute()
    {
        const string json = "{\"Name\":\"ColorTest\",\"Color\":\"Green\"}";
        var deserializedValue = JsonSerializer.Deserialize<TestType>(json);
        Assert.Equal("ColorTest", deserializedValue.Name);
        Assert.Equal(Color.Green, deserializedValue.Color);
    }

    private record TestType(string Name, Color Color);

    [JsonConverter(typeof(GenericEnumJsonConverter<Color>))]
    private enum Color {
        [EnumMember(Value="definitely-not-red")]
        Red, 
        Green };
}
