using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Common.Serialization;

namespace Dapr.Actors.Benchmarks;

public sealed class BenchmarkAotWireSerializer : IActorWireSerializer
{
    public byte[] JsonToBytes(string? json) =>
        string.IsNullOrEmpty(json) ? [] : Encoding.UTF8.GetBytes(json);

    public string? BytesToJson(ReadOnlyMemory<byte> bytes) =>
        bytes.IsEmpty ? null : Encoding.UTF8.GetString(bytes.Span);

    public byte[] SerializeToBytes<T>(T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, typeof(T), BenchmarkAotJsonContext.Default);

    public T? DeserializeFromBytes<T>(ReadOnlyMemory<byte> bytes) =>
        bytes.IsEmpty ? default : (T?)JsonSerializer.Deserialize(bytes.Span, typeof(T), BenchmarkAotJsonContext.Default);
}

public sealed class BenchmarkAotDaprSerializer : IDaprSerializer
{
    public string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, typeof(T), BenchmarkAotJsonContext.Default);

    [RequiresUnreferencedCode("Runtime-type serialization is not used by this AOT benchmark.")]
    [RequiresDynamicCode("Runtime-type serialization is not used by this AOT benchmark.")]
    public string Serialize(object? value, Type? inputType = null) =>
        JsonSerializer.Serialize(value, inputType ?? value?.GetType() ?? typeof(object), BenchmarkAotJsonContext.Default);

    public T? Deserialize<T>(string? data) =>
        string.IsNullOrEmpty(data) ? default : (T?)JsonSerializer.Deserialize(data, typeof(T), BenchmarkAotJsonContext.Default);

    [RequiresUnreferencedCode("Runtime-type deserialization is not used by this AOT benchmark.")]
    [RequiresDynamicCode("Runtime-type deserialization is not used by this AOT benchmark.")]
    public object? Deserialize(string? data, Type returnType) =>
        string.IsNullOrEmpty(data) ? null : JsonSerializer.Deserialize(data, returnType, BenchmarkAotJsonContext.Default);
}

[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(object))]
internal sealed partial class BenchmarkAotJsonContext : JsonSerializerContext;
