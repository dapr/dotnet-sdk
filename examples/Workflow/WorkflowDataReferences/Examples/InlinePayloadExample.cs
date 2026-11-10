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
using Microsoft.Extensions.DependencyInjection;
using WorkflowDataReferences.Workflows;

namespace WorkflowDataReferences.Examples;

internal sealed class InlinePayloadExample : PayloadExample
{
    public override string Id => "inline";
    public override string Title => "Inline payloads (no binary store)";
    public override string WhenToUse => "Small values should travel directly through history; ordinary typed reads are enough.";

    public override void ConfigureServices(IServiceCollection services)
    {
        // Workflow/activity discovery is source-generated. No WithBinaryStorePayloads means no offloading.
        services.AddDaprWorkflow();
    }

    public override async Task RunAsync(
        IServiceProvider services, DocumentPayload document, CancellationToken cancellationToken)
    {
        Console.WriteLine("Registration: AddDaprWorkflow(...) only; payload storage is not enabled.");
        Console.WriteLine("History: full serialized documents in workflow and activity inputs/outputs.");
        Console.WriteLine("Application code: ordinary DocumentPayload objects; no storage calls or references.");

        var client = services.GetRequiredService<DaprWorkflowClient>();
        var instanceId = $"payload-inline-{Guid.NewGuid():N}";
        Console.WriteLine($"Workflow ID: {instanceId}");
        await client.ScheduleNewWorkflowAsync(nameof(DocumentProcessingWorkflow), instanceId, document,
            startTime: null, cancellation: cancellationToken);
        // Request values because ReadOutputAs<DocumentPayload>() needs the serialized output.
        // ReadWorkflowOutputChunksAsync is not an alternative for inline output.
        var state = await WaitForCompletionAsync(client, instanceId, getInputsAndOutputs: true, cancellationToken);
        PrintDocumentResult(state, document);
        Console.WriteLine("Takeaway: the document itself travels through workflow history.");
    }
}
