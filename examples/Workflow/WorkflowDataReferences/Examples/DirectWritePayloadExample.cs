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

internal sealed class DirectWritePayloadExample : PayloadExample
{
    public override string Id => "direct-write";
    public override string Title => "Activity produces pages directly into a binary-store upload";
    public override string WhenToUse => "Upload while receiving data, with bounded memory and no temporary files.";

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

    public override async Task RunAsync(IServiceProvider services, DocumentPayload document,
        CancellationToken cancellationToken)
    {
        const int pageCount = SamplePayloadData.PageCount;
        var client = services.GetRequiredService<DaprWorkflowClient>();
        var instanceId = $"payload-direct-{Guid.NewGuid():N}";
        Console.WriteLine($"Workflow ID: {instanceId}");
        Console.WriteLine("Activity generates 512 x 64 KiB pages (32 MiB), uploading each as it arrives.");
        await client.ScheduleNewWorkflowAsync(nameof(DirectWritePayloadWorkflow), instanceId, pageCount, null, cancellation: cancellationToken);
        await WaitForCompletionAsync(client, instanceId, getInputsAndOutputs: false, cancellationToken);
        var reference = await client.GetWorkflowOutputReferenceAsync(instanceId, cancellationToken);
        Console.WriteLine($"Stored key: {reference.Key}");

        var resolver = services.GetRequiredService<IWorkflowPayloadResolver>();
        // Sample-only full-read verification; streaming reads do not verify integrity automatically.
        await SamplePayloadData.VerifyPagesAsync(
            resolver.ReadChunksAsync(reference, cancellationToken), reference, pageCount);

        Console.WriteLine($"Verified {reference.Size:N0} bytes. Activity and workflow outputs contain only the reference.");
        Console.WriteLine($"Reference SHA-256: {reference.ContentHash}; size: {reference.Size:N0} bytes.");
        Console.WriteLine("No temporary files. Retries create new keys, not content-addressed duplicates.");
    }
}
