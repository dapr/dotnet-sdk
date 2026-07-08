using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Interpreted;
using Dapr.Workflow;
using Moq;

namespace Dapr.Actors.Next.Examples.Approvals.Tests;

public sealed class VerificationTests
{
    private static readonly ActorId Expense = ActorId.Create("exp-1");

    [Fact]
    public void Well_formed_definition_passes_verification()
    {
        var verifier = new InterpretedMachineVerifier(Registry());

        Assert.True(ApprovalDefinitions.Verify(verifier, ApprovalDefinitions.ExpenseReport()).IsValid);
        Assert.True(ApprovalDefinitions.Verify(verifier, ApprovalDefinitions.Contract()).IsValid);
    }

    [Fact]
    public void Verification_rejects_definition_that_can_strand_an_approved_document()
    {
        var verifier = new InterpretedMachineVerifier(Registry());
        var stranded = ApprovalDefinitions.ExpenseReport(includeSettlementCompletion: false);

        var result = ApprovalDefinitions.Verify(verifier, stranded);

        Assert.False(result.IsValid);
        Assert.Contains(result.Defects, defect => defect.Contains("State 'Approved' is a dead end", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Definition_referencing_an_unregistered_effect_is_rejected_before_rollout()
    {
        var registry = Registry();
        var deployer = new InterpretedMachineDeployer(new InterpretedMachineVerifier(registry), new InMemoryInterpretedMachineStore());
        var definition = ApprovalDefinitions.ExpenseReport(settlementEffect: "UnknownSettlement");

        Assert.False(registry.TryGetEffect("UnknownSettlement", out _));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            deployer.DeployAsync(ApprovalDefinitions.ActorType, Expense, definition).AsTask());
        Assert.Contains("Effect 'UnknownSettlement'", ex.Message, StringComparison.Ordinal);
    }

    // The verifier only needs the registry to resolve capability names; the workflow client is never called.
    private static ApprovalCapabilityRegistry Registry() =>
        new(Mock.Of<IDaprWorkflowClient>());
}
