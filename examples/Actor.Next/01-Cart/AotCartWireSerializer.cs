using System.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Common.Serialization;

namespace Dapr.Actors.Next.Examples.Cart;

public sealed class AotCartWireSerializer : IActorWireSerializer
{
    public byte[] JsonToBytes(string? json) =>
        string.IsNullOrEmpty(json) ? [] : Encoding.UTF8.GetBytes(json);

    public string? BytesToJson(ReadOnlyMemory<byte> bytes) =>
        bytes.IsEmpty ? null : Encoding.UTF8.GetString(bytes.Span);

    public byte[] SerializeToBytes<T>(T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, typeof(T), AotCartJsonContext.Default);

    public T? DeserializeFromBytes<T>(ReadOnlyMemory<byte> bytes) =>
        bytes.IsEmpty ? default : (T?)JsonSerializer.Deserialize(bytes.Span, typeof(T), AotCartJsonContext.Default);
}

public sealed class AotCartDaprSerializer : IDaprSerializer
{
    public string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, typeof(T), AotCartJsonContext.Default);

    [RequiresUnreferencedCode("Runtime-type serialization is not used by this AOT example.")]
    [RequiresDynamicCode("Runtime-type serialization is not used by this AOT example.")]
    public string Serialize(object? value, Type? inputType = null) =>
        JsonSerializer.Serialize(value, inputType ?? value?.GetType() ?? typeof(object), AotCartJsonContext.Default);

    public T? Deserialize<T>(string? data) =>
        string.IsNullOrEmpty(data) ? default : (T?)JsonSerializer.Deserialize(data, typeof(T), AotCartJsonContext.Default);

    [RequiresUnreferencedCode("Runtime-type deserialization is not used by this AOT example.")]
    [RequiresDynamicCode("Runtime-type deserialization is not used by this AOT example.")]
    public object? Deserialize(string? data, Type returnType) =>
        string.IsNullOrEmpty(data) ? null : JsonSerializer.Deserialize(data, returnType, AotCartJsonContext.Default);
}

[JsonSerializable(typeof(ActorStateEnvelope<CartState>))]
[JsonSerializable(typeof(CartState))]
[JsonSerializable(typeof(CartItem))]
[JsonSerializable(typeof(CartSummary))]
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(Dictionary<string, decimal>))]
[JsonSerializable(typeof(object))]
internal sealed partial class AotCartJsonContext : JsonSerializerContext;
