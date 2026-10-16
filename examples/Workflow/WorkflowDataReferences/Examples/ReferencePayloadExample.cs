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
using WorkflowDataReferences.Activities;
using WorkflowDataReferences.Workflows;

namespace WorkflowDataReferences.Examples;

internal sealed class ReferencePayloadExample : PayloadExample
{
    public override string Title => "Explicit references (workflow forwards, activity streams)";

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDaprWorkflow(options =>
        {
            options.RegisterWorkflow<StreamingPayloadWorkflow>();
            options.RegisterActivity<MeasurePayloadActivity>();
        }).WithBinaryStorePayloads(options =>
        {
            options.StoreName = "workflow-payloads";
            options.KeyPrefix = "payload-example";
            options.OffloadThreshold = 64 * 1024;
        });
    }

    public override async Task RunAsync(
        IServiceProvider services, DocumentPayload document, CancellationToken cancellationToken)
    {
        Console.WriteLine("Registration: the same binary-store support as automatic offloading.");
        Console.WriteLine("History: an explicit PayloadReference<DocumentPayload> input and a small inline summary output.");
        Console.WriteLine("Application code: the client uploads once; the workflow forwards the reference without reading the document.");

        var client = services.GetRequiredService<DaprWorkflowClient>();
        var resolver = services.GetRequiredService<IWorkflowPayloadResolver>();
        var instanceId = $"payload-ref-{Guid.NewGuid():N}";
        Console.WriteLine($"Workflow ID: {instanceId}");

        // Upload from client or activity code, never from a replayed workflow orchestrator.
        var inputReference = await resolver.StoreAsync(document, instanceId, cancellationToken);
        Console.WriteLine($"Client stored {inputReference.Size} serialized bytes; input reference: {inputReference.ToEnvelope()}");
        await client.ScheduleNewWorkflowAsync(nameof(StreamingPayloadWorkflow), instanceId, inputReference,
            startTime: null, cancellation: cancellationToken);
        var state = await WaitForCompletionAsync(client, instanceId, cancellationToken);
        var summary = state.ReadOutputAs<PayloadSummary>()
                      ?? throw new InvalidOperationException("The streaming workflow returned no summary.");
        if (summary.ByteCount != inputReference.Size || summary.Sha256 != inputReference.ContentHash)
            throw new InvalidOperationException("The streaming result does not match the stored document.");

        Console.WriteLine($"Result: activity streamed {summary.ByteCount} serialized bytes using a 16 KiB buffer.");
        Console.WriteLine($"SHA-256 verified: {summary.Sha256}");
        Console.WriteLine("Serialized bytes include JSON structure, not just the document's characters.");
        Console.WriteLine("Client inspection: fetching inputs/outputs at completion also resolves the referenced input on the client.");
        Console.WriteLine("Takeaway: the workflow never reads the document; the activity streams bytes instead of deserializing it.");
    }
}
