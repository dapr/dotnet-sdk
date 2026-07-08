using System.Text.Json;
using Approvals.Next.Example06.Effects;
using Approvals.Next.Example06.Guards;
using Dapr.Actors.Next.Abstractions.Filters;
using Dapr.Actors.Next.Interpreted;
using Dapr.Workflow;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dapr.Actors.Next.Examples.Approvals;

/// <summary>
/// The vetted guards and effects an interpreted approval machine may reference by name. A definition
/// can only compose from these, so a typo or an unsupported action fails verification at onboarding
/// rather than on a live document.
/// </summary>
public sealed class ApprovalCapabilityRegistry : ICapabilityRegistry
{
    /// <summary>The auto-approval limit; amounts at or below it skip manager escalation.</summary>
    public const decimal AutoApprovalLimit = 1_000m;

    private readonly Dictionary<string, IActorEffect> _effects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IActorGuard> _guards = new(StringComparer.Ordinal);

    public ApprovalCapabilityRegistry(IDaprWorkflowClient workflowClient, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(workflowClient);
        var logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<ApprovalCapabilityRegistry>();

        _effects[ApprovalDefinitions.RecordSubmissionEffect] = new RecordSubmissionEffect();
        _effects[ApprovalDefinitions.RecordDecisionEffect] = new RecordDecisionEffect();
        _effects[ApprovalDefinitions.NotifyManagerEffect] = new LogEffect(logger, "escalated to a manager");
        _effects[ApprovalDefinitions.RecordSettlementFailureEffect] = new RecordSettlementFailureEffect();
        _effects[ApprovalDefinitions.StartSettlementEffect] = new StartSettlementEffect(workflowClient, logger);

        _guards[ApprovalDefinitions.WithinApprovalLimitGuard] = new WithinApprovalLimitGuard();
    }

    public bool TryGetEffect(string name, out IActorEffect effect) => _effects.TryGetValue(name, out effect!);

    public bool TryGetGuard(string name, out IActorGuard guard) => _guards.TryGetValue(name, out guard!);

    internal static DynamicStateBag State(ActorCapabilityContext context) =>
        (DynamicStateBag)context.Arguments["state"]!;

    internal static JsonElement Payload(ActorCapabilityContext context) =>
        (JsonElement)context.Arguments["payload"]!;
}
