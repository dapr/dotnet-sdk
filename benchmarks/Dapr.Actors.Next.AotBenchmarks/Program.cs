using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Attributes;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Core.Activation;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Common.Serialization;
using Microsoft.Extensions.DependencyInjection;

const int DefaultIterations = 10_000;
const int ParallelCalls = 32;

var iterations = args.Length > 0 && int.TryParse(args[0], out var parsed) ? parsed : DefaultIterations;
var payload = "1"u8.ToArray();
IReadOnlyDictionary<string, string> headers = new Dictionary<string, string>();

var startup = Stopwatch.StartNew();
using var provider = CreateServiceProvider();
startup.Stop();

var runtime = provider.GetRequiredService<IActorRuntime>();
var first = await MeasureAsync(() => InvokeAsync(runtime, "first"));
await InvokeAsync(runtime, "warm");
var warm = await MeasureAsync(async () =>
{
    var total = 0;
    for (var i = 0; i < iterations; i++)
    {
        total += await InvokeAsync(runtime, "warm");
    }

    return total;
});
var cold = await MeasureAsync(async () =>
{
    var total = 0;
    for (var i = 0; i < iterations; i++)
    {
        total += await InvokeAsync(runtime, "cold-" + i);
    }

    return total;
});
var parallel = await MeasureAsync(async () =>
{
    var total = 0;
    for (var batch = 0; batch < iterations / ParallelCalls; batch++)
    {
        var tasks = new Task<int>[ParallelCalls];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = InvokeAsync(runtime, "parallel-" + batch + "-" + i);
        }

        total += (await Task.WhenAll(tasks)).Sum();
    }

    return total;
});

Console.WriteLine("Dapr.Actors.Next Native AOT benchmark");
Console.WriteLine($"Iterations: {iterations}");
Console.WriteLine($"Startup: {startup.Elapsed.TotalMilliseconds:N3} ms");
Console.WriteLine($"First call: {first.Elapsed.TotalMilliseconds:N3} ms, allocated {first.AllocatedBytes:N0} B");
Console.WriteLine($"Warm calls: {warm.Elapsed.TotalMilliseconds:N3} ms, allocated {warm.AllocatedBytes:N0} B, checksum {warm.Result}");
Console.WriteLine($"Cold activations: {cold.Elapsed.TotalMilliseconds:N3} ms, allocated {cold.AllocatedBytes:N0} B, checksum {cold.Result}");
Console.WriteLine($"Parallel fan-out: {parallel.Elapsed.TotalMilliseconds:N3} ms, allocated {parallel.AllocatedBytes:N0} B, checksum {parallel.Result}");

static ServiceProvider CreateServiceProvider()
{
    var services = new ServiceCollection();
    services.AddSingleton<IDaprSerializer, AotBenchmarkDaprSerializer>();
    services.AddSingleton<IActorWireSerializer, AotBenchmarkWireSerializer>();
    services.AddDaprActors(options =>
    {
        options.EnableAutoActorRegistration = false;
        options.DisableStateMigration = true;
        options.Actors.RegisterActor<AotBenchmarkActor>("AotBenchmarkActor");
    });

    return services.BuildServiceProvider();
}

async Task<int> InvokeAsync(IActorRuntime runtime, string actorId)
{
    var response = await runtime.InvokeAsync("AotBenchmarkActor", actorId, nameof(IAotBenchmarkActor.AddAsync), payload, headers);
    return response?.Length ?? 0;
}

static async Task<Measurement> MeasureAsync(Func<Task<int>> action)
{
    var before = GC.GetAllocatedBytesForCurrentThread();
    var stopwatch = Stopwatch.StartNew();
    var result = await action().ConfigureAwait(false);
    stopwatch.Stop();
    var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    return new Measurement(stopwatch.Elapsed, allocated, result);
}

internal readonly record struct Measurement(TimeSpan Elapsed, long AllocatedBytes, int Result);

[GenerateActorClient]
public interface IAotBenchmarkActor : IActor
{
    Task<int> AddAsync(int value, CancellationToken cancellationToken = default);
}

[DaprActor("AotBenchmarkActor")]
public sealed class AotBenchmarkActor(ActorActivationContext context) : Actor, IAotBenchmarkActor
{
    private int value;

    protected override ActorId Id => context.ActorId;

    protected override IActorStateAccessor State => context.State;

    public Task<int> AddAsync(int value, CancellationToken cancellationToken = default)
    {
        this.value += value;
        return Task.FromResult(this.value);
    }
}

public sealed class AotBenchmarkWireSerializer : IActorWireSerializer
{
    public byte[] JsonToBytes(string? json) =>
        string.IsNullOrEmpty(json) ? [] : Encoding.UTF8.GetBytes(json);

    public string? BytesToJson(ReadOnlyMemory<byte> bytes) =>
        bytes.IsEmpty ? null : Encoding.UTF8.GetString(bytes.Span);

    public byte[] SerializeToBytes<T>(T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, typeof(T), AotBenchmarkJsonContext.Default);

    public T? DeserializeFromBytes<T>(ReadOnlyMemory<byte> bytes) =>
        bytes.IsEmpty ? default : (T?)JsonSerializer.Deserialize(bytes.Span, typeof(T), AotBenchmarkJsonContext.Default);
}

public sealed class AotBenchmarkDaprSerializer : IDaprSerializer
{
    public string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, typeof(T), AotBenchmarkJsonContext.Default);

    [RequiresUnreferencedCode("Runtime-type serialization is not used by this AOT benchmark.")]
    [RequiresDynamicCode("Runtime-type serialization is not used by this AOT benchmark.")]
    public string Serialize(object? value, Type? inputType = null) =>
        JsonSerializer.Serialize(value, inputType ?? value?.GetType() ?? typeof(object), AotBenchmarkJsonContext.Default);

    public T? Deserialize<T>(string? data) =>
        string.IsNullOrEmpty(data) ? default : (T?)JsonSerializer.Deserialize(data, typeof(T), AotBenchmarkJsonContext.Default);

    [RequiresUnreferencedCode("Runtime-type deserialization is not used by this AOT benchmark.")]
    [RequiresDynamicCode("Runtime-type deserialization is not used by this AOT benchmark.")]
    public object? Deserialize(string? data, Type returnType) =>
        string.IsNullOrEmpty(data) ? null : JsonSerializer.Deserialize(data, returnType, AotBenchmarkJsonContext.Default);
}

[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(object))]
internal sealed partial class AotBenchmarkJsonContext : JsonSerializerContext;
