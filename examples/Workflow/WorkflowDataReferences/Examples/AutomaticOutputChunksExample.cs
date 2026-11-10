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

internal sealed class AutomaticOutputChunksExample : PayloadExample
{
    public override string Id => "auto-output-chunks";
    public override string Title => "Automatic offloading with a chunked client read";
    public override string WhenToUse => "The worker produces ordinary objects, but the client needs raw output bytes incrementally.";

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDaprWorkflow().WithBinaryStorePayloads(options =>
        {
            options.StoreName = "workflow-payloads";
            options.KeyPrefix = "payload-example";
            options.WritePreference = PayloadWritePreference.PreferReference;
            options.OffloadSites = PayloadKind.WorkflowInput | PayloadKind.ActivityInput |
                                   PayloadKind.ActivityOutput | PayloadKind.WorkflowOutput;
            // Chunk reads require an actual persisted output reference. Always offload so the
            // client read contract still works if someone replaces the sample with a tiny document.
            options.OffloadThreshold = null;
        });
    }

    public override async Task RunAsync(
        IServiceProvider services, DocumentPayload document, CancellationToken cancellationToken)
    {
        Console.WriteLine("Producer: the SAME object-based workflow/activity as auto-values; worker allocations are unchanged.");
        Console.WriteLine("Consumer: raw serialized JSON chunks, not a DocumentPayload or just its Content field.");

        var client = services.GetRequiredService<DaprWorkflowClient>();
        var instanceId = $"payload-auto-output-chunks-{Guid.NewGuid():N}";
        Console.WriteLine($"Workflow ID: {instanceId}");
        await client.ScheduleNewWorkflowAsync(nameof(DocumentProcessingWorkflow), instanceId, document,
            startTime: null, cancellation: cancellationToken);

        // Requesting normal values here would hydrate them before we ever got to the chunk loop.
        // This workflow keeps custom status inline; the existing wait APIs can still hydrate custom status.
        await WaitForCompletionAsync(client, instanceId, getInputsAndOutputs: false, cancellationToken);

        // Chunks are byte boundaries, not JSON records or necessarily complete UTF-8 characters.
        long byteCount = 0;
        var chunkCount = 0;
        await foreach (var chunk in client.ReadWorkflowOutputChunksAsync(instanceId, 64 * 1024, cancellationToken))
        {
            byteCount += chunk.Length;
            chunkCount++;
        }
        if (byteCount == 0)
            throw new InvalidOperationException("The serialized document output was unexpectedly empty.");

        Console.WriteLine($"Result: read {byteCount:N0} JSON bytes in {chunkCount} sized chunks; no output object was deserialized.");
        Console.WriteLine("Chunk reads do not automatically verify integrity.");
        Console.WriteLine("Takeaway: automatic references support chunked reads too, but object-based uploads still materialize data.");
    }
}
