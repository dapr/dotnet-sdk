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

/// <summary>Demonstrates explicit client prefix metadata and internally supplied activity metadata for two tenants.</summary>
/// <remarks>The factory uses only the public prefix context; workflow and activity implementations are unchanged.</remarks>
internal sealed class TenantPrefixPayloadExample : PayloadExample
{
    /// <inheritdoc />
    public override string Id => "tenant-prefix";
    /// <inheritdoc />
    public override string Title => "Route payloads by tenant, workflow, and activity";
    /// <inheritdoc />
    public override string WhenToUse => "Use prefix metadata to organize payloads without changing workflow or activity code.";

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDaprWorkflow().WithBinaryStorePayloads(options =>
        {
            options.StoreName = "workflow-payloads";
            options.KeyPrefix = "workflow-payloads";
            options.PrefixFactory = metadata =>
            {
                // Application convention: tenant--execution, independent of ambient request state.
                var instanceId = metadata.InstanceId;
                var separator = instanceId.IndexOf("--", StringComparison.Ordinal);
                if (separator <= 0 || !instanceId[..separator].All(char.IsAsciiLetterOrDigit))
                    throw new ArgumentException("Expected an alphanumeric tenant followed by '--' in the instance ID.");
                // Activities always supply their own name, but may not know the owning workflow's name.
                var scope = metadata.ActivityName is { } activity ? $"activities/{activity}"
                    : metadata.WorkflowName is { } workflow ? $"workflows/{workflow}"
                    : throw new InvalidOperationException("This policy requires a workflow or activity name.");
                return $"tenants/{instanceId[..separator]}/{scope}/instances";
            };
            options.WritePreference = PayloadWritePreference.Preserve;
            options.OffloadSites = PayloadKind.None;
        });
    }

    /// <inheritdoc />
    public override async Task RunAsync(IServiceProvider services, DocumentPayload document,
        CancellationToken cancellationToken)
    {
        var client = services.GetRequiredService<DaprWorkflowClient>();
        var resolver = services.GetRequiredService<IWorkflowPayloadResolver>();
        foreach (var tenant in new[] { "acme", "contoso" })
        {
            var instanceId = $"{tenant}--{Guid.NewGuid():N}";
            Console.WriteLine($"Tenant: {tenant}; workflow ID: {instanceId}");
            var metadata = new WorkflowPayloadPrefixContext(instanceId, workflowName: nameof(DirectWritePayloadWorkflow));
            var input = await resolver.StoreAsync(1, metadata, cancellationToken);
            var inputPrefix = $"workflow-payloads/tenants/{tenant}/workflows/{metadata.WorkflowName}/instances/{instanceId}/";
            if (!input.Key.StartsWith(inputPrefix, StringComparison.Ordinal))
                throw new InvalidOperationException($"Expected input key beneath '{inputPrefix}', got '{input.Key}'.");
            Console.WriteLine($"Input key: {input.Key}");
            await client.ScheduleNewWorkflowAsync(nameof(DirectWritePayloadWorkflow), instanceId, input,
                startTime: null, cancellation: cancellationToken);
            await WaitForCompletionAsync(client, instanceId, getInputsAndOutputs: false, cancellationToken);

            var reference = await client.GetWorkflowOutputReferenceAsync(instanceId, cancellationToken);
            var expectedPrefix = $"workflow-payloads/tenants/{tenant}/activities/{nameof(WritePagedPayloadActivity)}/instances/{instanceId}/uploads/";
            if (!reference.Key.StartsWith(expectedPrefix, StringComparison.Ordinal))
                throw new InvalidOperationException($"Expected payload key beneath '{expectedPrefix}', got '{reference.Key}'.");
            Console.WriteLine($"Stored key: {reference.Key}");
        }

        Console.WriteLine("Client supplies workflow metadata; activity-context uploads supply their activity name automatically.");
        Console.WriteLine("The factory receives immutable prefix metadata, not the live activity context.");
        Console.WriteLine("Output inspection downloads only the reference, not the payload.");
        Console.WriteLine("Prefixes organize data; they are not an authorization boundary.");
    }
}
