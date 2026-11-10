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

using System.Security.Cryptography;
using Dapr.Workflow;
using Dapr.Workflow.Payloads;
using Microsoft.Extensions.DependencyInjection;
using WorkflowDataReferences.Workflows;

namespace WorkflowDataReferences.Examples;

internal sealed class StreamUploadPayloadExample(bool automaticReferences = false) : PayloadExample
{
    // The two selectable scenarios share the same receiver and read path.
    // Only the client's upload API and sending policy differ.
    public override string Id => automaticReferences ? "auto-stream" : "explicit-stream";
    public override string Title => automaticReferences
        ? "Automatic stream references (pass stream, receive reference)"
        : "Stream uploads (file input and streamed activity output)";
    public override string WhenToUse => automaticReferences
        ? "Pass a Stream directly to scheduling; receive raw output in chunks without client resolver calls."
        : "Explicitly store raw stream bytes and keep all workflow/activity boundaries reference-typed.";

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDaprWorkflow().WithBinaryStorePayloads(options =>
        {
            options.StoreName = "workflow-payloads";
            options.KeyPrefix = "payload-example";
            // Automatic Stream interception applies only to initial scheduling. Activity output is
            // still explicitly stored by CopyStreamPayloadActivity in BOTH scenarios.
            options.WritePreference = automaticReferences ? PayloadWritePreference.PreferReference : PayloadWritePreference.Preserve;
            options.OffloadSites = automaticReferences ? PayloadKind.WorkflowInput : PayloadKind.None;
        });
    }

    public override async Task RunAsync(
        IServiceProvider services, DocumentPayload document, CancellationToken cancellationToken)
    {
        Console.WriteLine("Input: a generated 32 MiB binary file, not a materialized document.");
        Console.WriteLine("StoreStreamAsync stages to temporary disk with a bounded buffer before uploading.");
        Console.WriteLine("History: references at all four workflow/activity input/output boundaries.");
        if (automaticReferences)
            Console.WriteLine("The client passes the FileStream directly to ScheduleNewWorkflowAsync; the SDK uploads it.");

        var client = services.GetRequiredService<DaprWorkflowClient>();
        var instanceId = $"payload-{(automaticReferences ? "auto-stream" : "stream")}-{Guid.NewGuid():N}";
        Console.WriteLine($"Workflow ID: {instanceId}");

        // Generate test data with one reusable buffer. Replace this with File.OpenRead or an HTTP
        // response stream in an application; seeking and a known source length are not required.
        await using var source = new FileStream(
            Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);
        var buffer = new byte[64 * 1024];
        for (var index = 0; index < buffer.Length; index++)
            buffer[index] = (byte)(index % 251);
        using var sourceHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (var chunk = 0; chunk < 512; chunk++)
        {
            await source.WriteAsync(buffer, cancellationToken);
            sourceHash.AppendData(buffer);
        }
        await source.FlushAsync(cancellationToken);
        source.Position = 0;
        var expectedHash = Convert.ToHexString(sourceHash.GetHashAndReset()).ToLowerInvariant();

        if (automaticReferences)
        {
            // Upload finishes before scheduling. A top-level Stream always becomes a reference,
            // regardless of threshold; the caller owns the stream and must keep it open until awaited.
            await client.ScheduleNewWorkflowAsync(nameof(StreamUploadWorkflow), instanceId, source,
                startTime: null, cancellation: cancellationToken);
        }
        else
        {
            // StoreStreamAsync stores bytes as-is, not JSON. Use an untyped PayloadReference:
            // arbitrary binary bytes cannot safely be read as a DocumentPayload.
            var resolver = services.GetRequiredService<IWorkflowPayloadResolver>();
            var stored = await resolver.StoreStreamAsync(source, instanceId, cancellationToken);
            await client.ScheduleNewWorkflowAsync(nameof(StreamUploadWorkflow), instanceId, stored,
                startTime: null, cancellation: cancellationToken);
        }

        // Do not request normal input/output metadata: it can hydrate referenced values.
        // This workflow sets no custom status, so waiting does not hydrate any payload.
        await WaitForCompletionAsync(client, instanceId, getInputsAndOutputs: false, cancellationToken);

        // Partial consumption is supported. Exiting await foreach closes the download, though
        // transport may have prefetched data. Chunks need not be accumulated or copied into one array.
        await foreach (var chunk in client.ReadWorkflowOutputChunksAsync(instanceId, 64 * 1024, cancellationToken))
        {
            Console.WriteLine($"Preview: read {chunk.Length:N0} bytes, then close the download without reading the rest.");
            break;
        }

        // A new enumeration starts from the beginning. Read all chunks here only to verify the sample.
        long byteCount = 0;
        using var outputHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await foreach (var chunk in client.ReadWorkflowOutputChunksAsync(instanceId, cancellationToken))
        {
            // Chunk memory may be reused on the next iteration; consume it now, not later.
            byteCount += chunk.Length;
            outputHash.AppendData(chunk.Span);
        }
        var actualHash = Convert.ToHexString(outputHash.GetHashAndReset()).ToLowerInvariant();
        if (actualHash != expectedHash)
            throw new InvalidOperationException($"Output SHA-256 mismatch: expected {expectedHash}, got {actualHash}.");
        if (byteCount != source.Length)
            throw new InvalidOperationException($"Expected {source.Length} bytes, but read {byteCount}.");

        Console.WriteLine($"Result: streamed and verified {byteCount:N0} raw bytes.");
        Console.WriteLine(automaticReferences
            ? "The client uploads automatically; the activity calls StoreStreamAsync; the workflow only forwards references."
            : "Client and activity both use StoreStreamAsync; the workflow only forwards references.");
        Console.WriteLine("Identical bytes in the same instance reuse the content-addressed key.");
        Console.WriteLine("The source and SDK staging files are deleted on disposal; stored payloads remain.");
        Console.WriteLine("Client output chunks bypass payload hydration at every size; no resolver call is needed.");
    }
}
