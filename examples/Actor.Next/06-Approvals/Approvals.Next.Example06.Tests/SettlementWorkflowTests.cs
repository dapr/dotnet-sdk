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

using Dapr.Actors.Next.Examples.Approvals.Workflows;
using Dapr.Actors.Next.Examples.Approvals.Workflows.Activities;
using Dapr.Workflow;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Dapr.Actors.Next.Examples.Approvals.Tests;

public sealed class SettlementWorkflowTests
{
    [Fact]
    public async Task Successful_charge_notifies_every_party_then_archives_the_document()
    {
        var input = new SettlementInput("exp-1", "ExpenseReport", "alice", 250m, ["finance", "alice"], SimulateChargeFailure: false);
        var context = MockContext();
        // Charge succeeds.
        context.Setup(ctx => ctx.CallActivityAsync(nameof(ChargeOrProvisionActivity), It.IsAny<ChargeRequest>(), It.IsAny<WorkflowTaskOptions>()))
            .Returns(Task.CompletedTask);

        var result = await new SettlementWorkflow().RunAsync(context.Object, input);

        Assert.True(result.Settled);
        Assert.Equal("Archived", result.FinalState);

        // Fan-out: one notification per party.
        context.Verify(ctx => ctx.CallActivityAsync(nameof(NotifyPartiesActivity), It.IsAny<PartyNotification>(), It.IsAny<WorkflowTaskOptions>()), Times.Exactly(2));
        // Success drives the document to the completed outcome, and never compensates.
        context.Verify(ctx => ctx.CallActivityAsync(nameof(SignalDocumentActivity), It.Is<DocumentSignal>(s => s.EventName == "SettlementCompleted"), It.IsAny<WorkflowTaskOptions>()), Times.Once());
        context.Verify(ctx => ctx.CallActivityAsync(nameof(ReleaseReservationActivity), It.IsAny<ReleaseRequest>(), It.IsAny<WorkflowTaskOptions>()), Times.Never());
    }

    [Fact]
    public async Task Charge_that_exhausts_its_retries_compensates_and_fails_the_document()
    {
        var input = new SettlementInput("exp-2", "ExpenseReport", "carol", 8500m, ["finance", "carol", "vendor"], SimulateChargeFailure: true);
        var context = MockContext();
        // Charge fails after its retries are exhausted (surfaced to the workflow as WorkflowTaskFailedException).
        context.Setup(ctx => ctx.CallActivityAsync(nameof(ChargeOrProvisionActivity), It.IsAny<ChargeRequest>(), It.IsAny<WorkflowTaskOptions>()))
            .Returns(Task.FromException(new WorkflowTaskFailedException("charge failed", new WorkflowTaskFailureDetails("Declined", "Charge was declined"))));

        var result = await new SettlementWorkflow().RunAsync(context.Object, input);

        Assert.False(result.Settled);
        Assert.Equal("SettlementFailed", result.FinalState);

        // Compensation ran, and the document was driven to the failed outcome (not archived).
        context.Verify(ctx => ctx.CallActivityAsync(nameof(ReleaseReservationActivity), It.IsAny<ReleaseRequest>(), It.IsAny<WorkflowTaskOptions>()), Times.Once());
        context.Verify(ctx => ctx.CallActivityAsync(nameof(SignalDocumentActivity), It.Is<DocumentSignal>(s => s.EventName == "SettlementFailed"), It.IsAny<WorkflowTaskOptions>()), Times.Once());
        context.Verify(ctx => ctx.CallActivityAsync(nameof(SignalDocumentActivity), It.Is<DocumentSignal>(s => s.EventName == "SettlementCompleted"), It.IsAny<WorkflowTaskOptions>()), Times.Never());
    }

    // A context whose fan-out, compensation, and signal activities all succeed by default; each test
    // overrides only the charge activity to choose the success or failure path.
    private static Mock<WorkflowContext> MockContext()
    {
        var context = new Mock<WorkflowContext>();
        context.Setup(ctx => ctx.CreateReplaySafeLogger<SettlementWorkflow>()).Returns(NullLogger<SettlementWorkflow>.Instance);
        context.Setup(ctx => ctx.CallActivityAsync(nameof(NotifyPartiesActivity), It.IsAny<PartyNotification>(), It.IsAny<WorkflowTaskOptions>()))
            .Returns(Task.CompletedTask);
        context.Setup(ctx => ctx.CallActivityAsync(nameof(ReleaseReservationActivity), It.IsAny<ReleaseRequest>(), It.IsAny<WorkflowTaskOptions>()))
            .Returns(Task.CompletedTask);
        context.Setup(ctx => ctx.CallActivityAsync(nameof(SignalDocumentActivity), It.IsAny<DocumentSignal>(), It.IsAny<WorkflowTaskOptions>()))
            .Returns(Task.CompletedTask);
        return context;
    }
}
