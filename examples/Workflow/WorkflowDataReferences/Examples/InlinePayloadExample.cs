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
using WorkflowDataReferences.Activities;
using WorkflowDataReferences.Workflows;

namespace WorkflowDataReferences.Examples;

internal sealed class InlinePayloadExample : PayloadExample
{
    public override string Title => "Inline payloads (no binary store)";

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDaprWorkflow(options =>
        {
            options.RegisterWorkflow<DocumentProcessingWorkflow>();
            options.RegisterActivity<UppercaseDocumentActivity>();
        });
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
        var state = await WaitForCompletionAsync(client, instanceId, cancellationToken);
        PrintDocumentResult(state, document);
        Console.WriteLine("Takeaway: the document itself travels through workflow history.");
    }
}
