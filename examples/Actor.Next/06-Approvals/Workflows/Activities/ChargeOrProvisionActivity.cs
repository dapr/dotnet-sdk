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

namespace Dapr.Actors.Next.Examples.Approvals.Workflows.Activities;

/// <summary>
/// Charges (or provisions) for an approved document. This is the failure-prone external step: when the
/// request is marked to simulate a failure it throws, so the workflow's retry policy re-invokes it and,
/// once retries are exhausted, the workflow compensates. In a real deployment this would call a payment
/// or provisioning service and must be idempotent.
/// </summary>
public sealed partial class ChargeOrProvisionActivity(ILogger<ChargeOrProvisionActivity> logger) : WorkflowActivity<ChargeRequest, object?>
{
    public override Task<object?> RunAsync(WorkflowActivityContext context, ChargeRequest input)
    {
        if (input.SimulateFailure)
        {
            LogChargeFailed(input.DocumentId);
            throw new InvalidOperationException($"Charge for document '{input.DocumentId}' was declined");
        }

        LogChargeSucceeded(input.Amount, input.DocumentId);
        return Task.FromResult<object?>(null);
    }

    [LoggerMessage(LogLevel.Warning, "Charge for {DocumentId} failed (simulated)")]
    private partial void LogChargeFailed(string documentId);

    [LoggerMessage(LogLevel.Information, "Charged {Amount:C} for document {DocumentId}")]
    private partial void LogChargeSucceeded(decimal amount, string documentId);
}
