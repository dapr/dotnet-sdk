using Dapr.Actors.Next.Abstractions.State.Versioning;

namespace Dapr.Actors.Next.Core.Serialization;

/// <summary>
/// Serializes actor payloads at the runtime wire boundary.
/// </summary>
public interface IActorWireSerializer : IActorStateMigrationSerializer
{
    /// <inheritdoc />
    string IActorStateMigrationSerializer.SerializerId => "dapr-json";

    /// <inheritdoc />
    int IActorStateMigrationSerializer.SerializerVersion => 1;

    /// <summary>
    /// Converts JSON text to UTF-8 wire bytes.
    /// </summary>
    byte[] JsonToBytes(string? json);

    /// <summary>
    /// Converts UTF-8 wire bytes to JSON text.
    /// </summary>
    string? BytesToJson(ReadOnlyMemory<byte> bytes);

    /// <summary>
    /// Serializes a value to UTF-8 wire bytes.
    /// </summary>
    new byte[] SerializeToBytes<T>(T value);

    /// <summary>
    /// Deserializes a value from UTF-8 wire bytes.
    /// </summary>
    new T? DeserializeFromBytes<T>(ReadOnlyMemory<byte> bytes);
}
