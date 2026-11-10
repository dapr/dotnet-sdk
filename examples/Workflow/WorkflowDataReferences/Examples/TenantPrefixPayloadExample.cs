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

internal sealed class TenantPrefixPayloadExample : PayloadExample
{
    public override string Id => "tenant-prefix";
    public override string Title => "Route the same workflow's payloads by tenant and instance";
    public override string WhenToUse => "Organize stored payloads by tenant without changing activity or workflow code.";

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDaprWorkflow().WithBinaryStorePayloads(options =>
        {
            options.StoreName = "workflow-payloads";
            options.KeyPrefix = "workflow-payloads";
            options.InstancePrefixFactory = instanceId =>
            {
                // Application convention: tenant--execution, independent of ambient request state.
                var separator = instanceId.IndexOf("--", StringComparison.Ordinal);
                if (separator <= 0 || !instanceId[..separator].All(char.IsAsciiLetterOrDigit))
                    throw new ArgumentException("Expected an alphanumeric tenant followed by '--' in the instance ID.");
                return $"tenants/{instanceId[..separator]}/instances";
            };
            options.WritePreference = PayloadWritePreference.Preserve;
            options.OffloadSites = PayloadKind.None;
        });
    }

    public override async Task RunAsync(IServiceProvider services, DocumentPayload document,
        CancellationToken cancellationToken)
    {
        var client = services.GetRequiredService<DaprWorkflowClient>();
        foreach (var tenant in new[] { "acme", "contoso" })
        {
            var instanceId = $"{tenant}--{Guid.NewGuid():N}";
            Console.WriteLine($"Tenant: {tenant}; workflow ID: {instanceId}");
            await client.ScheduleNewWorkflowAsync(nameof(DirectWritePayloadWorkflow), instanceId, 1,
                startTime: null, cancellation: cancellationToken);
            await WaitForCompletionAsync(client, instanceId, getInputsAndOutputs: false, cancellationToken);

            var reference = await client.GetWorkflowOutputReferenceAsync(instanceId, cancellationToken);
            var expectedPrefix = $"workflow-payloads/tenants/{tenant}/instances/{instanceId}/uploads/";
            if (!reference.Key.StartsWith(expectedPrefix, StringComparison.Ordinal))
                throw new InvalidOperationException($"Expected payload key beneath '{expectedPrefix}', got '{reference.Key}'.");
            Console.WriteLine($"Stored key: {reference.Key}");
        }

        Console.WriteLine("Same workflow and activity, separate tenant/instance paths. No payload download needed.");
        Console.WriteLine("Prefixes organize data; they are not an authorization boundary.");
    }
}
