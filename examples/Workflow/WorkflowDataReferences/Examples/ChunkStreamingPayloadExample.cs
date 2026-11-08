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

using Dapr.Workflow;
using Dapr.Workflow.Payloads;
using Microsoft.Extensions.DependencyInjection;
using WorkflowDataReferences.Workflows;

namespace WorkflowDataReferences.Examples;

internal sealed class ChunkStreamingPayloadExample : PayloadExample
{
    public override string Title => "Explicit chunks (workflow and activity both stream)";

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDaprWorkflow().WithBinaryStorePayloads(options =>
        {
            options.StoreName = "workflow-payloads";
            options.KeyPrefix = "payload-example";
            options.WritePreference = PayloadWritePreference.Preserve;
            options.OffloadSites = PayloadKind.None;
        });
    }

    public override async Task RunAsync(
        IServiceProvider services, DocumentPayload document, CancellationToken cancellationToken)
    {
        Console.WriteLine("Registration: Preserve with OffloadSites=None; no automatic boundary offloading.");
        Console.WriteLine("Application code: the client explicitly stores the document and passes its reference.");
        Console.WriteLine("Workflow: request 4 MiB chunks with context.ReadPayloadChunksAsync, checking the byte count.");
        Console.WriteLine("The 128 KiB sample fits in one final partial chunk; the SDK owns the buffer and stream.");
        Console.WriteLine("Activity: await foreach over resolver.ReadChunksAsync(input), checking the byte count and SHA-256.");

        var client = services.GetRequiredService<DaprWorkflowClient>();
        var resolver = services.GetRequiredService<IWorkflowPayloadResolver>();
        var instanceId = $"payload-chunks-{Guid.NewGuid():N}";
        Console.WriteLine($"Workflow ID: {instanceId}");

        var inputReference = await resolver.StoreAsync(document, instanceId, cancellationToken);
        Console.WriteLine($"Client stored the document; input reference: {inputReference.ToEnvelope()}");
        await client.ScheduleNewWorkflowAsync(nameof(ChunkStreamingWorkflow), instanceId, inputReference,
            startTime: null, cancellation: cancellationToken);
        var state = await WaitForCompletionAsync(client, instanceId, cancellationToken);
        var summary = ReadReferenceResult(state, inputReference);

        Console.WriteLine($"Result: workflow and activity each streamed {summary.ByteCount} serialized bytes.");
        Console.WriteLine($"Activity SHA-256 verified: {summary.Sha256}");
        Console.WriteLine("Replay reads the workflow's chunks again; keep the referenced bytes immutable and available.");
        Console.WriteLine("Client completion inspection also resolves the input; this is not a single-download example.");
        Console.WriteLine("Takeaway: explicit chunk reads work in both workflow and activity code without deserializing the document.");
    }
}
