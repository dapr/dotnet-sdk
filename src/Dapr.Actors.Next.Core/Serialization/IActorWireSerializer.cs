namespace Dapr.Actors.Next.Core.Serialization;

/// <summary>
/// Serializes actor payloads at the runtime wire boundary.
/// </summary>
public interface IActorWireSerializer
{
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
    byte[] SerializeToBytes<T>(T value);

    /// <summary>
    /// Deserializes a value from UTF-8 wire bytes.
    /// </summary>
    T? DeserializeFromBytes<T>(ReadOnlyMemory<byte> bytes);
}
