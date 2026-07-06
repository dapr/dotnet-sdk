using Microsoft.CodeAnalysis;

namespace Dapr.Actors.Next.Analyzers;

/// <summary>
/// Defines diagnostics produced by the Dapr Actors Next analyzers.
/// </summary>
public static class ActorAnalyzerDiagnostics
{
    /// <summary>
    /// Gets the analyzer id range reserved for Dapr Actors Next.
    /// </summary>
    public const string ReservedRange = "DAPR1410-DAPR1428";

    /// <summary>
    /// Diagnostic raised when a shipped actor state shape is changed in a breaking way.
    /// </summary>
    public static readonly DiagnosticDescriptor StateShapeChanged = new(
        "DAPR1410",
        "Actor state shape change breaks shipped serialization",
        "State type '{0}' no longer matches the shipped state baseline: {1}",
        "Compatibility",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    /// <summary>
    /// Diagnostic raised when work escapes the actor scheduler.
    /// </summary>
    public static readonly DiagnosticDescriptor SchedulerEscape = new(
        "DAPR1411",
        "Actor turn must not escape the scheduler",
        "Avoid '{0}' inside an actor turn because it escapes the actor scheduler",
        "Concurrency",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Diagnostic raised when actor code blocks the cooperative scheduler.
    /// </summary>
    public static readonly DiagnosticDescriptor BlockingCall = new(
        "DAPR1412",
        "Actor turn must not block",
        "Avoid blocking call '{0}' inside an actor turn",
        "Concurrency",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Diagnostic raised when actor code reads wall-clock time directly.
    /// </summary>
    public static readonly DiagnosticDescriptor DirectTime = new(
        "DAPR1413",
        "Actor turn must use TimeProvider",
        "Use an injected TimeProvider instead of '{0}' inside an actor turn",
        "Determinism",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Diagnostic raised when actor code uses an unseeded nondeterministic source.
    /// </summary>
    public static readonly DiagnosticDescriptor NondeterministicSource = new(
        "DAPR1414",
        "Actor turn must use a scheduler-aware seeded source",
        "Use a scheduler-aware seeded source instead of '{0}' inside an actor turn",
        "Determinism",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Diagnostic raised when a migration target cannot be reached because no upcaster migrates to it.
    /// </summary>
    public static readonly DiagnosticDescriptor BrokenUpcasterChain = new(
        "DAPR1415",
        "Actor state migration target is unreachable",
        "No upcaster migrates to '{0}'; did you intend one from '{1}'",
        "Compatibility",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    /// <summary>
    /// Diagnostic raised when a global turn filter appears to contain actor business logic.
    /// </summary>
    public static readonly DiagnosticDescriptor BusinessLogicInFilter = new(
        "DAPR1416",
        "Actor turn filter should stay cross-cutting",
        "Move business logic '{0}' out of IActorTurnFilter and into the actor",
        "Design",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <summary>
    /// Diagnostic raised when an actor interface method has an unsupported return type.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidActorMethodReturnType = new(
        "DAPR1417",
        "Actor interface method must return an asynchronous type",
        "Actor interface method '{0}' must return Task, ValueTask, or IAsyncEnumerable<T>",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Diagnostic raised when a shipped actor interface wire contract is edited in place.
    /// </summary>
    public static readonly DiagnosticDescriptor WireContractChanged = new(
        "DAPR1418",
        "Actor interface change breaks shipped wire contract",
        "Actor interface '{0}' no longer matches the shipped wire baseline: {1}",
        "Compatibility",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Diagnostic raised when an actor stores mutable shared state in an instance field.
    /// </summary>
    public static readonly DiagnosticDescriptor MutableActorField = new(
        "DAPR1419",
        "Actor field should not hold mutable shared state",
        "Make actor field '{0}' readonly or move mutable state into actor state storage",
        "Concurrency",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Diagnostic raised when multiple actor implementations that share a contract register the same actor type.
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateActorTypeName = new(
        "DAPR1420",
        "Actor type name must disambiguate shared actor contracts",
        "Actor type name '{0}' is used by multiple actors implementing '{1}'; set distinct DaprActor names or explicit registration aliases",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    /// <summary>
    /// Diagnostic raised when a Dapr actor implementation has no generated actor client contract.
    /// </summary>
    public static readonly DiagnosticDescriptor MissingGeneratedActorClient = new(
        "DAPR1421",
        "Actor implementation must expose a generated client contract",
        "Actor '{0}' must implement an interface decorated with GenerateActorClientAttribute",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Diagnostic raised when a state type looks like part of a migration family but is not connected.
    /// </summary>
    public static readonly DiagnosticDescriptor UnconnectedStateFamilyMember = new(
        "DAPR1423",
        "Actor state type is not connected to its migration family",
        "State type '{0}' looks like it belongs to migration family '{1}' but no mapping connects it",
        "Compatibility",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    /// <summary>
    /// Diagnostic raised when two ordered migration fragments have no connecting hop.
    /// </summary>
    public static readonly DiagnosticDescriptor UpcasterChainGap = new(
        "DAPR1424",
        "Actor state migration chain has a gap",
        "Actor state migration family '{0}' has no hop from '{1}' to '{2}'",
        "Compatibility",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    /// <summary>
    /// Diagnostic raised when a consecutive state step is not additive and needs a hand-authored upcaster.
    /// </summary>
    public static readonly DiagnosticDescriptor NonAdditiveMigrationStep = new(
        "DAPR1425",
        "Actor state migration step requires an upcaster",
        "State migration from '{0}' to '{1}' is not additive and cannot be generated automatically",
        "Compatibility",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    /// <summary>
    /// Diagnostic raised when a migration graph has more than one path to a target.
    /// </summary>
    public static readonly DiagnosticDescriptor NonUniqueFoldPath = new(
        "DAPR1426",
        "Actor state migration fold path is ambiguous",
        "Actor state migration family '{0}' has more than one fold path to '{1}'",
        "Compatibility",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    /// <summary>
    /// Diagnostic raised when one persisted state name is used with multiple migration families.
    /// </summary>
    public static readonly DiagnosticDescriptor MultipleFamiliesForStateName = new(
        "DAPR1427",
        "Actor state name maps to multiple migration families",
        "State name '{0}' is used with multiple actor state families: {1}",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    /// <summary>
    /// Diagnostic raised when actor code uses an older state type while a later reachable version exists.
    /// </summary>
    public static readonly DiagnosticDescriptor OutdatedStateTypeUsage = new(
        "DAPR1428",
        "Actor state usage should target the latest state version",
        "State usage targets '{0}', but later state version '{1}' exists in the application",
        "Usage",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);
}
