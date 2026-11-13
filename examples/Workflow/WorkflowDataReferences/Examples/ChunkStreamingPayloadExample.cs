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

/// <summary>Demonstrates replay-aware workflow reads of immutable payload chunks.</summary>
internal sealed class ChunkStreamingPayloadExample : PayloadExample
{
    /// <inheritdoc />
    public override string Id => "workflow-chunks";
    /// <inheritdoc />
    public override string Title => "Advanced: chunks inside a replayed workflow";
    /// <inheritdoc />
    public override string WhenToUse => "The orchestrator itself must inspect immutable bytes, accepting replay-time downloads.";

    /// <inheritdoc />
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

    /// <inheritdoc />
    public override async Task RunAsync(
        IServiceProvider services, DocumentPayload document, CancellationToken cancellationToken)
    {
        Console.WriteLine("Registration: Preserve with OffloadSites=None; no automatic boundary offloading.");
        Console.WriteLine("Application code: the client explicitly stores the document and passes its reference.");
        Console.WriteLine("Workflow: request 32 KiB chunks with context.ReadPayloadChunksAsync, checking the byte count.");
        Console.WriteLine("The sample spans multiple chunks; chunk boundaries must not drive orchestration decisions.");
        Console.WriteLine("Activity: await foreach over resolver.ReadChunksAsync(input), checking the byte count and SHA-256.");

        var client = services.GetRequiredService<DaprWorkflowClient>();
        var resolver = services.GetRequiredService<IWorkflowPayloadResolver>();
        var instanceId = $"payload-chunks-{Guid.NewGuid():N}";
        Console.WriteLine($"Workflow ID: {instanceId}");

        // This example isolates workflow-side reading, not streaming uploads: StoreAsync buffers JSON.
        var inputReference = await resolver.StoreAsync(document, instanceId, cancellationToken);
        Console.WriteLine($"Client stored the document; input reference: {inputReference.ToEnvelope()}");
        await client.ScheduleNewWorkflowAsync(nameof(ChunkStreamingWorkflow), instanceId, inputReference,
            startTime: null, cancellation: cancellationToken);
        var state = await WaitForCompletionAsync(client, instanceId, getInputsAndOutputs: true, cancellationToken);
        var summary = ReadReferenceResult(state, inputReference);

        Console.WriteLine($"Result: workflow and activity each streamed {summary.ByteCount} serialized bytes.");
        Console.WriteLine($"Activity SHA-256 verified: {summary.Sha256}");
        Console.WriteLine("Replay reads the workflow's chunks again; keep the referenced bytes immutable and available.");
        Console.WriteLine("Client completion inspection also resolves the input; this is not a single-download example.");
        Console.WriteLine("Takeaway: explicit chunk reads work in both workflow and activity code without deserializing the document.");
    }
}
