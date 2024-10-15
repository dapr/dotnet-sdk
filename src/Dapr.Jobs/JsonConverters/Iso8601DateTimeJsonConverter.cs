using System.Text.Json.Serialization;
using System.Text.Json;

namespace Dapr.Jobs.JsonConverters;

/// <summary>
/// Converts from an ISO 8601 DateTime to a string and back. This is primarily used to serialize
/// dates for use with CosmosDB.
/// </summary>
public sealed class Iso8601DateTimeJsonConverter : JsonConverter<DateTimeOffset?>
{
    /// <summary>Reads and converts the JSON to a <see cref="DateTimeOffset"/>.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The converted value.</returns>
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        var dateString = reader.GetString();
        if (DateTimeOffset.TryParse(dateString, out var dateTimeOffset))
            return dateTimeOffset;

        throw new JsonException($"Unable to convert \"{dateString}\" to {nameof(DateTimeOffset)}");
    }

    /// <summary>Writes a specified value as JSON.</summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to convert to JSON.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value is not null)
        {
            writer.WriteStringValue(value.Value.ToString("O"));
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
