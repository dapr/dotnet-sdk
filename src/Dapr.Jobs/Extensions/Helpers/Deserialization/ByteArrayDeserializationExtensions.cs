using System.Text;
using System.Text.Json;

namespace Dapr.Jobs.Extensions.Helpers.Deserialization;

/// <summary>
/// Provides utility extensions for deserializing an array of UTF-8 encoded bytes.
/// </summary>
public static class ByteArrayDeserializationExtensions
{
    /// <summary>
    /// Local JSON serializer option defaults.
    /// </summary>
    private static readonly JsonSerializerOptions defaultOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    /// <summary>
    /// Deserializes an array of UTF-8 encoded bytes to a string.
    /// </summary>
    /// <param name="bytes">The array of UTF-8 encoded bytes.</param>
    /// <returns>A decoded string.</returns>
    public static string DeserializeToString(this ReadOnlySpan<byte> bytes) => Encoding.UTF8.GetString(bytes);

    /// <summary>
    /// Attempts to deserialize an array of UTF-8 encoded bytes to the indicated type.
    /// </summary>
    /// <typeparam name="TJsonObject">The JSON-compatible type to deserialize to.</typeparam>
    /// <param name="bytes">The array of UTF-8 encoded bytes to deserialize.</param>
    /// <param name="jsonSerializerOptions">Optional options to use for the <see cref="JsonSerializer"/>.</param>
    /// <returns>The deserialized data.</returns>
    public static TJsonObject? DeserializeFromJsonBytes<TJsonObject>(this ReadOnlySpan<byte> bytes,
        JsonSerializerOptions? jsonSerializerOptions = null)
    {
        var serializerOptions = jsonSerializerOptions ?? defaultOptions;
        return JsonSerializer.Deserialize<TJsonObject>(bytes, serializerOptions);
    }
}
