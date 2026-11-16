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

/// <summary>Demonstrates automatic input offloading to a reference-typed workflow contract.</summary>
internal sealed class AutomaticReferencePayloadExample : PayloadExample
{
    /// <inheritdoc />
    public override string Id => "auto-reference";
    /// <inheritdoc />
    public override string Title => "Automatic references (pass document, receive reference)";
    /// <inheritdoc />
    public override string WhenToUse => "The caller has an object, but the workflow should receive and forward its reference.";

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDaprWorkflow().WithBinaryStorePayloads(options =>
        {
            options.StoreName = "workflow-payloads";
            options.KeyPrefix = "payload-example";
            // The following two lines are critical to this working automatically
            options.WritePreference = PayloadWritePreference.PreferReference;
            options.OffloadSites = PayloadKind.WorkflowInput;
            // The receiver's PayloadReference<T> type does not cause an upload.
            // Always offload here: a threshold could leave a small document inline and break that contract.
            options.OffloadThreshold = null;
        });
    }

    /// <inheritdoc />
    public override async Task RunAsync(
        IServiceProvider services, DocumentPayload document, CancellationToken cancellationToken)
    {
        Console.WriteLine("Registration: PreferReference for WorkflowInput, with no size threshold.");
        Console.WriteLine("History: an automatically created reference as input and a small inline summary as output.");
        Console.WriteLine(
            "Application code: pass DocumentPayload directly; the SAME streaming workflow receives PayloadReference<DocumentPayload>.");
        Console.WriteLine(
            "Reference creation: the sender's policy uploads the document; the receiver's type alone does not trigger it.");

        var client = services.GetRequiredService<DaprWorkflowClient>();
        var instanceId = $"payload-auto-ref-{Guid.NewGuid():N}";
        Console.WriteLine($"Workflow ID: {instanceId}");

        // This is still an in-memory object upload. For an unmaterialized source, see auto-stream.
        await client.ScheduleNewWorkflowAsync(nameof(StreamingPayloadWorkflow), instanceId, document,
            startTime: null, cancellation: cancellationToken);
        // The workflow returns a small inline summary, not an output reference. Reading this summary
        // through the normal metadata API also hydrates the input; the worker's reads were chunked.
        var state = await WaitForCompletionAsync(client, instanceId, getInputsAndOutputs: true, cancellationToken);
        var inputReference = state.ReadInputAs<PayloadReference<DocumentPayload>>()
                             ?? throw new InvalidOperationException("The workflow input was not offloaded.");
        Console.WriteLine($"SDK stored the document automatically; input reference: {inputReference.ToEnvelope()}");
        PrintReferenceResult(state, inputReference);
    }
}
