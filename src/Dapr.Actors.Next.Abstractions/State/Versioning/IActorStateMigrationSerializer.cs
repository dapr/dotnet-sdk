namespace Dapr.Actors.Next.Abstractions.State.Versioning;

/// <summary>
/// Serializes and deserializes closed actor state payload types for migration.
/// </summary>
public interface IActorStateMigrationSerializer
{
    /// <summary>
    /// Gets the serializer identity recorded in actor state headers.
    /// </summary>
    string SerializerId { get; }

    /// <summary>
    /// Gets the serializer version recorded in actor state headers.
    /// </summary>
    int SerializerVersion { get; }

    /// <summary>
    /// Deserializes bytes into a closed payload type.
    /// </summary>
    T? DeserializeFromBytes<T>(ReadOnlyMemory<byte> bytes);

    /// <summary>
    /// Serializes a closed payload type to bytes.
    /// </summary>
    byte[] SerializeToBytes<T>(T value);
}
