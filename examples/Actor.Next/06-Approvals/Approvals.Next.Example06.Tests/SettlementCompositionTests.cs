using System.Text.Json;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Filters;
using Dapr.Actors.Next.Interpreted;
using Dapr.Workflow;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Dapr.Actors.Next.Examples.Approvals.Tests;

public sealed class SettlementCompositionTests
{
    [Fact]
    public async Task Approving_schedules_settlement_with_a_deterministic_id_and_does_not_double_schedule()
    {
        var scheduled = new List<string>();
        var workflowClient = new Mock<IDaprWorkflowClient>();
        workflowClient
            .Setup(client => client.ScheduleNewWorkflowAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<object?>()))
            .Returns((string _, string? instanceId, object? _) =>
            {
                scheduled.Add(instanceId!);
                return Task.FromResult(instanceId!);
            });

        var effect = new global::Approvals.Next.Example06.Effects.StartSettlementEffect(workflowClient.Object, NullLogger.Instance);
        var context = ApprovedContext("exp-9", amount: 250m, parties: ["finance", "alice"]);

        // Run the entry effect twice, simulating an at-least-once re-run of the approving turn.
        await effect.ExecuteAsync(context);
        await effect.ExecuteAsync(context);

        var expected = global::Approvals.Next.Example06.Effects.StartSettlementEffect.InstanceIdFor(ApprovalDefinitions.ActorType, "exp-9");
        Assert.All(scheduled, id => Assert.Equal(expected, id));

        // Both runs use the same instance id, so Dapr's dedup-by-instance-id keeps a single workflow.
        Assert.Single(scheduled.Distinct());
    }

    private static ActorCapabilityContext ApprovedContext(string documentId, decimal amount, string[] parties)
    {
        var bag = new DynamicStateBag();
        bag.Set("documentType", "ExpenseReport");
        bag.Set("requester", "alice");
        bag.Set("amount", amount);
        bag.Set("parties", parties);
        bag.Set("simulateChargeFailure", false);

        return new ActorCapabilityContext(
            ApprovalDefinitions.ActorType,
            ActorId.Create(documentId),
            ApprovalDefinitions.StartSettlementEffect,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["state"] = bag,
                ["event"] = "Approve",
                ["payload"] = JsonSerializer.SerializeToElement(new { }),
                ["documentVersion"] = 1,
            },
            new ActorRequestContext(null, null, new Dictionary<string, string>(StringComparer.Ordinal)));
    }
}
