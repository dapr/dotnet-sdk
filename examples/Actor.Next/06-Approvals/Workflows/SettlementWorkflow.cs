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

using Dapr.Actors.Next.Examples.Approvals.Workflows.Activities;
using Dapr.Workflow;

namespace Dapr.Actors.Next.Examples.Approvals.Workflows;

/// <summary>
/// The finite, failure-prone process an approved document hands off to. It fans out notifications,
/// charges (or provisions) with a retry policy, and — if the charge cannot be made good — compensates
/// and drives the document to a failed outcome. This is the multi-step orchestration that belongs in a
/// workflow rather than an actor turn.
/// </summary>
public sealed partial class SettlementWorkflow : Workflow<SettlementInput, SettlementResult>
{
    private static readonly WorkflowTaskOptions ChargeRetry = new(
        new WorkflowRetryPolicy(
            maxNumberOfAttempts: 4,
            firstRetryInterval: TimeSpan.FromSeconds(2),
            backoffCoefficient: 2.0,
            maxRetryInterval: TimeSpan.FromSeconds(30)));

    public override async Task<SettlementResult> RunAsync(WorkflowContext context, SettlementInput input)
    {
        var logger = context.CreateReplaySafeLogger<SettlementWorkflow>();

        // 1. Fan out: notify every party in parallel.
        var notifications = input.Parties
            .Select(party => context.CallActivityAsync(
                nameof(NotifyPartiesActivity),
                new PartyNotification(input.DocumentId, party)))
            .ToList();
        await Task.WhenAll(notifications);

        // 2. Retry: charge (or provision) the failure-prone external step under a backoff policy.
        try
        {
            await context.CallActivityAsync(
                nameof(ChargeOrProvisionActivity),
                new ChargeRequest(input.DocumentId, input.Amount, input.SimulateChargeFailure),
                ChargeRetry);
        }
        catch (WorkflowTaskFailedException ex)
        {
            // 3. Compensate: the charge exhausted its retries, so undo any partial provisioning and
            // report the failure back to the document entity.
            LogSettlementFailed(logger, input.DocumentId, ex.FailureDetails.ErrorMessage);
            await context.CallActivityAsync(
                nameof(ReleaseReservationActivity),
                new ReleaseRequest(input.DocumentId, input.Amount));
            await context.CallActivityAsync(
                nameof(SignalDocumentActivity),
                new DocumentSignal(input.DocumentId, "SettlementFailed"));
            return new SettlementResult(Settled: false, FinalState: "SettlementFailed");
        }

        // 4. Success: drive the document entity to its archived outcome.
        await context.CallActivityAsync(
            nameof(SignalDocumentActivity),
            new DocumentSignal(input.DocumentId, "SettlementCompleted"));
        return new SettlementResult(Settled: true, FinalState: "Archived");
    }
    
    [LoggerMessage(LogLevel.Error, "Settlement for {DocumentId} failed: {Reason}. Compensating.")]
    private static partial void LogSettlementFailed(ILogger logger, string documentId, string reason);
}
