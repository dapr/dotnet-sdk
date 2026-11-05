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

internal sealed class AutomaticReferencePayloadExample : PayloadExample
{
    public override string Title => "Automatic references (pass document, receive reference)";

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDaprWorkflow().WithBinaryStorePayloads(options =>
        {
            options.StoreName = "workflow-payloads";
            options.KeyPrefix = "payload-example";
            options.WritePreference = PayloadWritePreference.PreferReference;
            options.OffloadSites = PayloadKind.WorkflowInput;
            // The workflow requires a reference even when the document is small.
            options.OffloadThreshold = null;
        });
    }

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

        await client.ScheduleNewWorkflowAsync(nameof(StreamingPayloadWorkflow), instanceId, document,
            startTime: null, cancellation: cancellationToken);
        var state = await WaitForCompletionAsync(client, instanceId, cancellationToken);
        var inputReference = state.ReadInputAs<PayloadReference<DocumentPayload>>()
                             ?? throw new InvalidOperationException("The workflow input was not offloaded.");
        Console.WriteLine($"SDK stored the document automatically; input reference: {inputReference.ToEnvelope()}");
        PrintReferenceResult(state, inputReference);
    }
}
