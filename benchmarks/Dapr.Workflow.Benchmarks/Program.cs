// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapr.BinaryStore;
using Dapr.Common.Serialization;
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow;
using Dapr.Workflow.Benchmarks;
using Dapr.Workflow.Payloads;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddCommandLine(args).Build();
var iterations = configuration.GetValue("Iterations", 5);
var warmupRounds = configuration.GetValue("WarmupRounds", 1);
var historyCache = configuration.GetValue("HistoryCache", true);
var endpoint = configuration["DAPR_GRPC_ENDPOINT"] ?? "http://localhost:50001";
var storeName = configuration["StoreName"] ?? "workflow-benchmark";
var output = configuration["Output"] ?? "workflow-data-references.json";
ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);
ArgumentOutOfRangeException.ThrowIfNegative(warmupRounds);
var document = BenchmarkDocument.Create();
var expectedChecksum = document.Checksum();
var samples = new List<Sample>();
var modes = Enum.GetValues<BenchmarkMode>();
using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; shutdown.Cancel(); };

Console.WriteLine($"2 MB serialized document; 9 sequential activities; history cache: {historyCache}.");
Console.WriteLine("Use a dedicated sidecar/app ID with no other workers. Traffic metrics are not network packet bytes.");
for (var round = 0; round < warmupRounds + iterations; round++)
{
    // Rotate order to avoid always giving the last case the warmest runtime/storage.
    for (var offset = 0; offset < modes.Length; offset++)
    {
        var mode = modes[(round + offset) % modes.Length];
        var sample = await RunAsync(mode, round - warmupRounds + 1, shutdown.Token);
        if (round >= warmupRounds)
            samples.Add(sample);
        Console.WriteLine($"{(round < warmupRounds ? "warmup" : "sample"),6} {mode,-10} " +
            $"total {sample.TotalMs,8:F1} ms; execution {sample.ExecutionMs,8:F1} ms; " +
            $"turns {sample.Measurements.Turns.Count}; document reads {sample.Measurements.DocumentDeserializations}; " +
            $"binary reads {sample.Measurements.BinaryReads}");
    }
}

var jsonOptions = new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
var report = new
{
    SchemaVersion = 1,
    TimestampUtc = DateTimeOffset.UtcNow,
    Framework = RuntimeInformation.FrameworkDescription,
    SdkAssemblyVersion = typeof(DaprWorkflowClient).Assembly.GetName().Version?.ToString(),
    OS = RuntimeInformation.OSDescription,
    Environment.ProcessorCount,
    SerializedDocumentBytes = BenchmarkDocument.SerializedSize,
    Activities = 9,
    HistoryCache = historyCache,
    Iterations = iterations,
    WarmupRounds = warmupRounds,
    Samples = samples
};
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, jsonOptions), shutdown.Token);

Console.WriteLine("\nMedians (time range is min..max total ms; allocations are process-wide managed bytes):");
Console.WriteLine("Mode        Total ms   Exec ms  Min..Max ms       Workflow protobuf B  Binary down B  Doc reads  Allocated B  Total speedup");
var baselineMedian = Median(samples.Where(s => s.Mode == BenchmarkMode.Inline).Select(s => s.TotalMs));
foreach (var mode in modes)
{
    var rows = samples.Where(s => s.Mode == mode).ToArray();
    Console.WriteLine($"{mode,-10} {Median(rows.Select(s => s.TotalMs)),9:F1} {Median(rows.Select(s => s.ExecutionMs)),9:F1} " +
        $"{rows.Min(s => s.TotalMs),7:F1}..{rows.Max(s => s.TotalMs),7:F1} " +
        $"{Median(rows.Select(s => (double)(s.Measurements.WorkflowSentProtobufBytes + s.Measurements.WorkflowReceivedProtobufBytes))),20:F0} " +
        $"{Median(rows.Select(s => (double)s.Measurements.BinaryDownloadBytes)),14:F0} " +
        $"{Median(rows.Select(s => (double)s.Measurements.DocumentDeserializations)),10:F0} " +
        $"{Median(rows.Select(s => (double)s.AllocatedBytes)),12:F0} " +
        $"{baselineMedian / Median(rows.Select(s => s.TotalMs)),13:F2}x");
}
Console.WriteLine($"Per-execution byte counts and all samples: {Path.GetFullPath(output)}");

async Task<Sample> RunAsync(BenchmarkMode mode, int iteration, CancellationToken cancellationToken)
{
    var window = new MeasurementWindow();
    var serializer = new MeasuringSerializer(window);
    using var channel = GrpcChannel.ForAddress(endpoint, new GrpcChannelOptions
    {
        MaxReceiveMessageSize = 32 * 1024 * 1024,
        MaxSendMessageSize = 32 * 1024 * 1024
    });
    var grpc = new TaskHubSidecarService.TaskHubSidecarServiceClient(
        channel.Intercept(new MeasuringInterceptor(window)));
    var builder = Host.CreateApplicationBuilder();
    builder.Configuration.AddConfiguration(configuration);
    builder.Logging.SetMinimumLevel(LogLevel.Warning);
    var workflowBuilder = builder.Services.AddDaprWorkflowBuilder(options =>
    {
        options.DisableStatefulHistory = !historyCache;
        options.RegisterWorkflow<ConnectionWarmupWorkflow>();
        options.RegisterWorkflow<MaterializedWorkflow>();
        options.RegisterWorkflow<ReferenceWorkflow>();
        options.RegisterActivity<CoordinateActivity>();
        options.RegisterActivity<ProcessDocumentActivity>();
        options.RegisterActivity<ProcessReferenceActivity>();
    }, (_, client) => client.UseSerializer(serializer));
    builder.Services.Replace(ServiceDescriptor.Singleton(grpc));
    builder.Services.Replace(ServiceDescriptor.Singleton<IDaprSerializer>(serializer));
    if (mode != BenchmarkMode.Inline)
    {
        workflowBuilder.WithBinaryStorePayloads(options =>
        {
            options.StoreName = storeName;
            options.WritePreference = mode == BenchmarkMode.Automatic
                ? PayloadWritePreference.PreferReference : PayloadWritePreference.Preserve;
            options.KeyPrefix = "benchmark";
            options.OffloadThreshold = 64 * 1024;
        });
        builder.Services.WithPayloadManifest<BenchmarkManifest>();
        builder.Services.Replace(ServiceDescriptor.Singleton<DaprBinaryStoreClient>(_ =>
        {
            var client = new DaprBinaryStoreClientBuilder(configuration);
            client.UseGrpcEndpoint(endpoint);
            return new MeasuringBinaryStoreClient(client.Build(), window);
        }));
    }

    using var host = builder.Build();
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeout.CancelAfter(TimeSpan.FromMinutes(2));
    var token = timeout.Token;
    await host.StartAsync(token);
    try
    {
        var client = host.Services.GetRequiredService<DaprWorkflowClient>();
        // Establish the work-item stream before starting the measurement window.
        var warmupId = $"benchmark-connect-{Guid.NewGuid():N}";
        await client.ScheduleNewWorkflowAsync(nameof(ConnectionWarmupWorkflow), warmupId, 1, null, token);
        EnsureCompleted(await client.WaitForWorkflowCompletionAsync(warmupId, false, token), warmupId);
        if (!await client.PurgeInstanceAsync(warmupId, token))
            throw new InvalidOperationException($"Could not purge connection warmup {warmupId}.");

        var instanceId = $"benchmark-{mode}-{Guid.NewGuid():N}";
        Console.WriteLine($"Running {instanceId}");
        var measurement = new Measurements();
        window.Start(measurement);
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var total = Stopwatch.StartNew();
        object input = document;
        if (mode != BenchmarkMode.Inline)
        {
            // Seed the same initial reference in both offloaded cases so upload/serialization
            // is measured separately from replay. Automatic still materializes ordinary inputs.
            input = await host.Services.GetRequiredService<IWorkflowPayloadResolver>()
                .StoreAsync(document, instanceId, token);
        }
        var preparationMs = total.Elapsed.TotalMilliseconds;
        var execution = Stopwatch.StartNew();
        var workflow = mode == BenchmarkMode.References ? nameof(ReferenceWorkflow) : nameof(MaterializedWorkflow);
        await client.ScheduleNewWorkflowAsync(workflow, instanceId, input, null, token);
        var completed = await client.WaitForWorkflowCompletionAsync(instanceId, false, token);
        execution.Stop();
        total.Stop();
        var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        window.Stop();
        EnsureCompleted(completed, instanceId);

        // Inspect raw metadata outside the window: SDK metadata hydration would otherwise
        // introduce an extra document download unrelated to workflow replay.
        var headers = new Metadata();
        if (configuration["DAPR_API_TOKEN"] is { Length: > 0 } apiToken)
            headers.Add("dapr-api-token", apiToken);
        var state = await grpc.GetInstanceAsync(
            new GetInstanceRequest { InstanceId = instanceId, GetInputsAndOutputs = true },
            new CallOptions(headers, cancellationToken: token));
        if (!state.Exists || JsonSerializer.Deserialize<string>(state.WorkflowState.Output) != expectedChecksum)
            throw new InvalidOperationException($"Incorrect checksum for {instanceId}.");
        ValidateMeasurements(mode, measurement);

        if (!await client.PurgeInstanceAsync(instanceId, token))
            throw new InvalidOperationException($"Could not purge completed benchmark {instanceId}.");
        if (mode != BenchmarkMode.Inline)
            await host.Services.GetRequiredService<WorkflowPayloadCleaner>().PurgeAsync(instanceId, token);

        return new Sample(mode, iteration, preparationMs, execution.Elapsed.TotalMilliseconds,
            total.Elapsed.TotalMilliseconds, allocatedBytes, measurement);
    }
    finally
    {
        window.Stop();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StopAsync(stop.Token);
    }
}

static void EnsureCompleted(Dapr.Workflow.WorkflowState state, string instanceId)
{
    if (state.RuntimeStatus != WorkflowRuntimeStatus.Completed)
        throw new InvalidOperationException($"{instanceId}: {state.RuntimeStatus}: {state.FailureDetails?.ErrorMessage}");
}

static void ValidateMeasurements(BenchmarkMode mode, Measurements measurements)
{
    if (measurements.ActivityExecutions != 9 || measurements.Turns.Count < 2)
        throw new InvalidOperationException("Expected nine activities and multiple orchestration executions; use a dedicated sidecar.");
    var workflowReads = measurements.Turns.Sum(t => t.DocumentDeserializations);
    if (workflowReads != (mode == BenchmarkMode.References ? 0 : measurements.Turns.Count) ||
        measurements.DocumentDeserializations != workflowReads + 1)
        throw new InvalidOperationException("Unexpected document materialization counts.");
    if (mode == BenchmarkMode.Inline && (measurements.BinaryReads != 0 || measurements.BinaryWrites != 0))
        throw new InvalidOperationException("The inline baseline accessed the binary store.");
    if (mode == BenchmarkMode.References &&
        (measurements.BinaryReads != 1 || measurements.BinaryWrites != 1 ||
         measurements.BinaryDownloadBytes != BenchmarkDocument.SerializedSize ||
         measurements.BinaryUploadBytes != BenchmarkDocument.SerializedSize))
        throw new InvalidOperationException("The reference case must upload once and retrieve once.");
    if (mode == BenchmarkMode.Automatic &&
        measurements.BinaryDownloadBytes != (long)measurements.DocumentDeserializations * BenchmarkDocument.SerializedSize)
        throw new InvalidOperationException("Automatic hydration download bytes do not match document materializations.");
}

static double Median(IEnumerable<double> values)
{
    var ordered = values.Order().ToArray();
    var middle = ordered.Length / 2;
    return ordered.Length % 2 == 0 ? (ordered[middle - 1] + ordered[middle]) / 2 : ordered[middle];
}

internal sealed record Sample(BenchmarkMode Mode, int Iteration, double PreparationMs, double ExecutionMs,
    double TotalMs, long AllocatedBytes, Measurements Measurements);
