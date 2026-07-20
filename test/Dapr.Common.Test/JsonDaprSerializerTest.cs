using System.Text.Json;
using Dapr.Common.Serialization;
using Xunit;

namespace Dapr.Common.Test;

public sealed class JsonDaprSerializerTest
{
    [Fact]
    public void Generic_methods_use_configured_json_options()
    {
        var serializer = new JsonDaprSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var json = serializer.Serialize(new SerializerWidget("abc"));
        var fromString = serializer.Deserialize<SerializerWidget>("""{"name":"abc"}""");
        var bytes = serializer.SerializeToUtf8Bytes(new SerializerWidget("abc"));
        var fromBytes = serializer.DeserializeFromUtf8Bytes<SerializerWidget>("""{"name":"abc"}"""u8);

        Assert.Equal("""{"name":"abc"}""", json);
        Assert.Equal("abc", fromString!.Name);
        Assert.Equal("""{"name":"abc"}""", JsonSerializer.Deserialize<JsonElement>(bytes).GetRawText());
        Assert.Equal("abc", fromBytes!.Name);
    }

    private sealed record SerializerWidget(string Name);
}
