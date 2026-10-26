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

internal sealed class AutomaticPayloadExample : PayloadExample
{
    public override string Title => "Automatic offloading (same application code)";

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDaprWorkflow(options =>
        {
            options.RegisterWorkflow<DocumentProcessingWorkflow>();
            options.RegisterActivity<UppercaseDocumentActivity>();
        }).WithBinaryStorePayloads(options =>
        {
            options.StoreName = "workflow-payloads";
            options.WritePreference = PayloadWritePreference.PreferReference;
            options.KeyPrefix = "payload-example";
            options.OffloadThreshold = 64 * 1024;
        });
    }

    public override async Task RunAsync(
        IServiceProvider services, DocumentPayload document, CancellationToken cancellationToken)
    {
        Console.WriteLine("Registration: AddDaprWorkflow(...).WithBinaryStorePayloads(...), threshold 64 KiB.");
        Console.WriteLine("History: reference envelopes for large payloads; serialized documents live in the binary store.");
        Console.WriteLine("Application code: the SAME workflow and activity as inline; the SDK uploads and resolves data.");

        var client = services.GetRequiredService<DaprWorkflowClient>();
        var instanceId = $"payload-auto-{Guid.NewGuid():N}";
        Console.WriteLine($"Workflow ID: {instanceId}");
        await client.ScheduleNewWorkflowAsync(nameof(DocumentProcessingWorkflow), instanceId, document,
            startTime: null, cancellation: cancellationToken);
        var state = await WaitForCompletionAsync(client, instanceId, cancellationToken);

        // The asynchronous wait resolves output for the normal typed read, while retaining its wire reference.
        PrintDocumentResult(state, document);
        var outputReference = state.ReadOutputAs<PayloadReference<DocumentPayload>>()
                              ?? throw new InvalidOperationException("The large output was not offloaded.");
        Console.WriteLine($"Persisted output reference: {outputReference.ToEnvelope()}");
        Console.WriteLine("Takeaway: smaller history does not mean zero document reads; the SDK still materializes ordinary inputs.");
    }
}
