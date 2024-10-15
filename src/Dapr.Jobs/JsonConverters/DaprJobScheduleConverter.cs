using System.Text.Json;
using System.Text.Json.Serialization;
using Dapr.Jobs.Models;

namespace Dapr.Jobs.JsonConverters;

internal sealed class DaprJobScheduleConverter : JsonConverter<DaprJobSchedule>
{
    /// <summary>Reads and converts the JSON to type <see cref="DaprJobSchedule"/>.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The converted value.</returns>
    public override DaprJobSchedule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var exprValue = reader.GetString();
        return exprValue is null ? null : DaprJobSchedule.FromExpression(exprValue);
    }

    /// <summary>Writes a specified value as JSON.</summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to convert to JSON.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void Write(Utf8JsonWriter writer, DaprJobSchedule value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ExpressionValue);
    }
}
