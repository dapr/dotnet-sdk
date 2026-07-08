using System.Reflection;
using System.Text.Json;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Filters;
using Dapr.Actors.Next.Abstractions.Registry;
using Dapr.Actors.Next.Interpreted;
using Dapr.Actors.Next.Testing;
using Dapr.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Dapr.Actors.Next.Examples.Approvals.Tests;

public sealed class ApprovalMachineTests
{
    private static readonly ActorId Expense = ActorId.Create("exp-1");

    [Fact]
    public async Task Small_expense_auto_approves_and_starts_settlement_workflow()
    {
        var scheduled = new List<string>();
        var workflowClient = WorkflowClientRecording(scheduled);

        var store = new InMemoryInterpretedMachineStore();
        await Deploy(store, workflowClient, ApprovalDefinitions.ExpenseReport());
        await using var runtime = CreateRuntime(store, workflowClient);
        var controlPlane = new ApprovalControlPlane(
            Provider(runtime).GetRequiredService<IActorRegistry>(),
            Provider(runtime).GetRequiredService<IDynamicActorClient>());

        Assert.Equal("Submitted", await Drive(runtime, controlPlane.SubmitAsync(Expense.Value,
            new SubmitDocument("alice", 250m, ["finance", "alice"]))));
        Assert.Equal("InReview", await Drive(runtime, controlPlane.BeginReviewAsync(Expense.Value)));
        Assert.Equal("Approved", await Drive(runtime, controlPlane.ApproveAsync(Expense.Value, new Decision("bob"))));

        // Entering Approved fired the StartSettlement effect, which scheduled the workflow with a
        // deterministic instance id derived from the actor type and document id.
        Assert.Equal(
            [global::Approvals.Next.Example06.Effects.StartSettlementEffect.InstanceIdFor(ApprovalDefinitions.ActorType, Expense.Value)],
            scheduled);
    }

    [Fact]
    public async Task Large_expense_escalates_to_a_manager_before_approval()
    {
        var scheduled = new List<string>();
        var workflowClient = WorkflowClientRecording(scheduled);

        var store = new InMemoryInterpretedMachineStore();
        await Deploy(store, workflowClient, ApprovalDefinitions.ExpenseReport());
        await using var runtime = CreateRuntime(store, workflowClient);
        var controlPlane = new ApprovalControlPlane(
            Provider(runtime).GetRequiredService<IActorRegistry>(),
            Provider(runtime).GetRequiredService<IDynamicActorClient>());

        await Drive(runtime, controlPlane.SubmitAsync(Expense.Value, new SubmitDocument("carol", 8500m, ["finance"])));
        await Drive(runtime, controlPlane.BeginReviewAsync(Expense.Value));

        // Over the auto-approval limit, so the first Approve escalates rather than approving.
        Assert.Equal("Escalated", await Drive(runtime, controlPlane.ApproveAsync(Expense.Value, new Decision("bob"))));
        Assert.Empty(scheduled);

        // The manager's approval completes it and starts settlement.
        Assert.Equal("Approved", await Drive(runtime, controlPlane.ApproveAsync(Expense.Value, new Decision("manager"))));
        Assert.Single(scheduled);
    }

    private static async Task<string?> Drive(ActorTestRuntime runtime, Task<string?> invoke)
    {
        await runtime.RunToIdle();
        var json = await invoke;
        if (json is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("State").GetString();
    }

    private static ActorTestRuntime CreateRuntime(IInterpretedMachineStore store, IDaprWorkflowClient workflowClient) =>
        new(services =>
        {
            services.AddSingleton<IActorRegistry>(new ApprovalTypeRegistry());
            services.AddSingleton(store);
            services.AddSingleton(workflowClient);
            services.AddSingleton<ICapabilityRegistry>(_ => new ApprovalCapabilityRegistry(workflowClient));
            services.AddDaprInterpretedActors(ApprovalDefinitions.ActorType);
        });

    private static async Task Deploy(InMemoryInterpretedMachineStore store, IDaprWorkflowClient workflowClient, InterpretedMachineDefinition definition)
    {
        var deployer = new InterpretedMachineDeployer(
            new InterpretedMachineVerifier(new ApprovalCapabilityRegistry(workflowClient)),
            store);
        await deployer.DeployAsync(ApprovalDefinitions.ActorType, Expense, definition);
    }

    private static IDaprWorkflowClient WorkflowClientRecording(List<string> scheduled)
    {
        var mock = new Mock<IDaprWorkflowClient>();
        mock.Setup(client => client.ScheduleNewWorkflowAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<object?>()))
            .Returns((string _, string? instanceId, object? _) =>
            {
                scheduled.Add(instanceId!);
                return Task.FromResult(instanceId!);
            });
        return mock.Object;
    }

    private static IServiceProvider Provider(ActorTestRuntime runtime) =>
        (IServiceProvider)runtime.GetType().GetField("provider", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
}
