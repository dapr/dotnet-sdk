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

/// <summary>Demonstrates explicit object storage and reference-typed workflow/activity inputs.</summary>
internal sealed class ReferencePayloadExample : PayloadExample
{
    /// <inheritdoc />
    public override string Id => "explicit-reference";
    /// <inheritdoc />
    public override string Title => "Explicit references (workflow forwards, activity streams)";
    /// <inheritdoc />
    public override string WhenToUse => "Choose exactly when to store an object and pass a typed reference to consumers.";

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDaprWorkflow().WithBinaryStorePayloads(options =>
        {
            options.StoreName = "workflow-payloads";
            options.KeyPrefix = "payload-example";
            // Explicit StoreAsync is independent of thresholds and automatic offload sites.
            options.WritePreference = PayloadWritePreference.Preserve;
            options.OffloadSites = PayloadKind.None;
        });
    }

    /// <inheritdoc />
    public override async Task RunAsync(
        IServiceProvider services, DocumentPayload document, CancellationToken cancellationToken)
    {
        Console.WriteLine("Registration: binary-store support with the default Preserve write preference.");
        Console.WriteLine(
            "History: an explicit PayloadReference<DocumentPayload> input and a small inline summary output.");
        Console.WriteLine(
            "Application code: the client uploads once; the workflow forwards the reference without reading the document.");
        Console.WriteLine(
            "Reference creation: StoreAsync explicitly uploads regardless of the automatic-offloading threshold.");

        var client = services.GetRequiredService<DaprWorkflowClient>();
        var resolver = services.GetRequiredService<IWorkflowPayloadResolver>();
        var instanceId = $"payload-ref-{Guid.NewGuid():N}";
        Console.WriteLine($"Workflow ID: {instanceId}");

        // StoreAsync serializes this already-materialized object. It is not a stream upload.
        // The <DocumentPayload> on the reference describes its serialized contents, not a loaded value.
        // Upload from client or activity code, never from a replayed workflow orchestrator.
        var inputReference = await resolver.StoreAsync(document, instanceId, cancellationToken);
        Console.WriteLine(
            $"Client stored {inputReference.Size} serialized bytes; input reference: {inputReference.ToEnvelope()}");
        await client.ScheduleNewWorkflowAsync(nameof(StreamingPayloadWorkflow), instanceId, inputReference,
            startTime: null, cancellation: cancellationToken);
        // Only the activity's download is streamed here. This metadata read retrieves the inline
        // summary and also hydrates the input. See explicit-stream for streaming client output.
        var state = await WaitForCompletionAsync(client, instanceId, getInputsAndOutputs: true, cancellationToken);
        PrintReferenceResult(state, inputReference);
    }
}
