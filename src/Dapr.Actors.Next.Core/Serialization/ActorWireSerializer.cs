using System.Text;
using Dapr.Common.Serialization;

namespace Dapr.Actors.Next.Core.Serialization;

/// <summary>
/// Adapts the string-based Dapr serializer contract to byte-oriented runtime payloads.
/// </summary>
/// <remarks>
/// This is the only Core type that transcodes between JSON strings and UTF-8 bytes. If
/// <see cref="IDaprSerializer"/> gains byte-native overloads later, this adapter is the seam to swap.
/// The default System.Text.Json serializer is detected as a documented fast-path hook, while preserving
/// the string contract until byte-native APIs exist.
/// </remarks>
public sealed class ActorWireSerializer : IActorWireSerializer
{
    private static readonly byte[] EmptyJsonBytes = Encoding.UTF8.GetBytes(string.Empty);
    private readonly IDaprSerializer serializer;
    private readonly JsonDaprSerializer? jsonSerializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActorWireSerializer"/> class.
    /// </summary>
    public ActorWireSerializer(IDaprSerializer serializer)
    {
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

        // When the configured serializer is exactly the default System.Text.Json implementation we can go
        // straight to/from UTF-8 bytes, dropping the two JSON-string round trips on the hot dispatch path.
        // Any custom serializer falls back to the string contract below.
        jsonSerializer = serializer.GetType() == typeof(JsonDaprSerializer) ? (JsonDaprSerializer)serializer : null;
    }

    /// <summary>
    /// Gets the serializer identity recorded in actor state headers.
    /// </summary>
    public string SerializerId => "dapr-json";

    /// <summary>
    /// Gets the serializer version recorded in actor state headers.
    /// </summary>
    public int SerializerVersion => 1;

    /// <summary>
    /// Gets a value indicating whether the configured serializer is the default System.Text.Json implementation.
    /// </summary>
    public bool IsDefaultSystemTextJson => jsonSerializer is not null;

    /// <inheritdoc />
    public byte[] JsonToBytes(string? json) =>
        string.IsNullOrEmpty(json) ? EmptyJsonBytes : Encoding.UTF8.GetBytes(json);

    /// <inheritdoc />
    public string? BytesToJson(ReadOnlyMemory<byte> bytes) =>
        bytes.IsEmpty ? null : Encoding.UTF8.GetString(bytes.Span);

    /// <inheritdoc />
    public byte[] SerializeToBytes<T>(T value) =>
        jsonSerializer is not null ? jsonSerializer.SerializeToUtf8Bytes(value) : JsonToBytes(serializer.Serialize(value));

    /// <inheritdoc />
    public T? DeserializeFromBytes<T>(ReadOnlyMemory<byte> bytes) =>
        jsonSerializer is not null ? jsonSerializer.DeserializeFromUtf8Bytes<T>(bytes.Span) : serializer.Deserialize<T>(BytesToJson(bytes));
}
