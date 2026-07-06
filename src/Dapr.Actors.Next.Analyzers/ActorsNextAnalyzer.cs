using System.Collections.Immutable;
using System.Collections.Concurrent;
using Dapr.Actors.Next.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Dapr.Actors.Next.Analyzers;

/// <summary>
/// Enforces deterministic, scheduler-aware, and compatibility-safe Dapr Actors Next authoring rules.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ActorsNextAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Gets the diagnostics supported by the analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
        ActorAnalyzerDiagnostics.StateShapeChanged,
        ActorAnalyzerDiagnostics.SchedulerEscape,
        ActorAnalyzerDiagnostics.BlockingCall,
        ActorAnalyzerDiagnostics.DirectTime,
        ActorAnalyzerDiagnostics.NondeterministicSource,
        ActorAnalyzerDiagnostics.BrokenUpcasterChain,
        ActorAnalyzerDiagnostics.BusinessLogicInFilter,
        ActorAnalyzerDiagnostics.InvalidActorMethodReturnType,
        ActorAnalyzerDiagnostics.WireContractChanged,
        ActorAnalyzerDiagnostics.MutableActorField,
        ActorAnalyzerDiagnostics.DuplicateActorTypeName,
        ActorAnalyzerDiagnostics.UnconnectedStateFamilyMember,
        ActorAnalyzerDiagnostics.UpcasterChainGap,
        ActorAnalyzerDiagnostics.NonAdditiveMigrationStep,
        ActorAnalyzerDiagnostics.NonUniqueFoldPath,
        ActorAnalyzerDiagnostics.MultipleFamiliesForStateName);

    /// <summary>
    /// Initializes analyzer actions.
    /// </summary>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static startContext =>
        {
            var baseline = ActorBaseline.Load(startContext.Options.AdditionalFiles, startContext.CancellationToken);
            var actorImplementations = new ConcurrentBag<ActorImplementationInfo>();
            var explicitActorNames = new ConcurrentBag<ExplicitActorName>();
            var stateTypes = new ConcurrentBag<INamedTypeSymbol>();
            var upcasters = new ConcurrentBag<UpcasterEdge>();
            var stateUsages = new ConcurrentBag<StateUsage>();
            startContext.RegisterOperationAction(ctx =>
            {
                AnalyzeInvocation(ctx);
                CollectExplicitActorRegistration(ctx, explicitActorNames);
                CollectStateUsage(ctx, stateUsages);
            }, OperationKind.Invocation);
            startContext.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
            startContext.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
            startContext.RegisterSymbolAction(ctx => AnalyzeNamedType(ctx, baseline, actorImplementations, stateTypes, upcasters), SymbolKind.NamedType);
            startContext.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
            startContext.RegisterCompilationEndAction(ctx =>
            {
                AnalyzeDuplicateActorTypeNames(ctx, actorImplementations, explicitActorNames);
                AnalyzeStateMigrationGraph(ctx, stateTypes, upcasters, stateUsages);
            });
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (!IsInsideActorTurn(invocation.SemanticModel, invocation.Syntax, context.CancellationToken))
        {
            return;
        }

        var method = invocation.TargetMethod;
        var containingType = method.ContainingType?.ToDisplayString();
        var methodName = method.Name;
        var displayName = method.ContainingType is null ? methodName : method.ContainingType.Name + "." + methodName;

        if (containingType == "System.Threading.Tasks.Task" && methodName == "Run" ||
            containingType == "System.Threading.ThreadPool" && (methodName.Contains("Queue", StringComparison.Ordinal) || methodName.Contains("Unsafe", StringComparison.Ordinal)))
        {
            context.ReportDiagnostic(Diagnostic.Create(ActorAnalyzerDiagnostics.SchedulerEscape, invocation.Syntax.GetLocation(), displayName));
            return;
        }

        if (containingType == "System.Threading.Thread" && methodName == "Sleep" ||
            methodName == "Wait" && method.ContainingType is not null && IsBlockingWaitType(method.ContainingType))
        {
            context.ReportDiagnostic(Diagnostic.Create(ActorAnalyzerDiagnostics.BlockingCall, invocation.Syntax.GetLocation(), displayName));
            return;
        }

        if (containingType == "System.Diagnostics.Stopwatch")
        {
            context.ReportDiagnostic(Diagnostic.Create(ActorAnalyzerDiagnostics.DirectTime, invocation.Syntax.GetLocation(), displayName));
            return;
        }

        if (containingType == "System.Guid" && methodName == "NewGuid")
        {
            context.ReportDiagnostic(Diagnostic.Create(ActorAnalyzerDiagnostics.NondeterministicSource, invocation.Syntax.GetLocation(), "Guid.NewGuid"));
        }
    }

    private static void CollectExplicitActorRegistration(OperationAnalysisContext context, ConcurrentBag<ExplicitActorName> explicitActorNames)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (method.Name != "RegisterActor" ||
            method.TypeArguments.Length != 1 ||
            method.ContainingType?.ToDisplayString() != "Dapr.Actors.Next.Abstractions.Options.DaprActorRegistrationCollection")
        {
            return;
        }

        var actorType = method.TypeArguments[0] as INamedTypeSymbol;
        if (actorType is null)
        {
            return;
        }

        foreach (var argument in invocation.Arguments)
        {
            if (argument.Parameter?.Name != "actorTypeName" ||
                argument.Value.ConstantValue is not { HasValue: true, Value: string value } ||
                string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            explicitActorNames.Add(new ExplicitActorName(actorType, value));
            return;
        }
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        if (!IsInsideActorTurn(creation.SemanticModel, creation.Syntax, context.CancellationToken))
        {
            return;
        }

        var typeName = creation.Type?.ToDisplayString();
        if (typeName == "System.Threading.Thread")
        {
            context.ReportDiagnostic(Diagnostic.Create(ActorAnalyzerDiagnostics.SchedulerEscape, creation.Syntax.GetLocation(), "new Thread"));
        }
        else if (typeName == "System.Diagnostics.Stopwatch")
        {
            context.ReportDiagnostic(Diagnostic.Create(ActorAnalyzerDiagnostics.DirectTime, creation.Syntax.GetLocation(), "Stopwatch"));
        }
        else if (typeName == "System.Random" && creation.Arguments.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(ActorAnalyzerDiagnostics.NondeterministicSource, creation.Syntax.GetLocation(), "new Random()"));
        }
    }

    private static void AnalyzePropertyReference(OperationAnalysisContext context)
    {
        var propertyReference = (IPropertyReferenceOperation)context.Operation;
        if (!IsInsideActorTurn(propertyReference.SemanticModel, propertyReference.Syntax, context.CancellationToken))
        {
            return;
        }

        var property = propertyReference.Property;
        var containingType = property.ContainingType?.ToDisplayString();

        if (property.Name == "Result" && IsTaskLike(property.ContainingType))
        {
            context.ReportDiagnostic(Diagnostic.Create(ActorAnalyzerDiagnostics.BlockingCall, propertyReference.Syntax.GetLocation(), ".Result"));
            return;
        }

        if ((containingType == "System.DateTime" || containingType == "System.DateTimeOffset") &&
            (property.Name == "Now" || property.Name == "UtcNow"))
        {
            context.ReportDiagnostic(Diagnostic.Create(ActorAnalyzerDiagnostics.DirectTime, propertyReference.Syntax.GetLocation(), containingType + "." + property.Name));
            return;
        }

        if (containingType == "System.Random" && property.Name == "Shared")
        {
            context.ReportDiagnostic(Diagnostic.Create(ActorAnalyzerDiagnostics.NondeterministicSource, propertyReference.Syntax.GetLocation(), "Random.Shared"));
        }
    }

    private static void AnalyzeNamedType(
        SymbolAnalysisContext context,
        ActorBaseline baseline,
        ConcurrentBag<ActorImplementationInfo> actorImplementations,
        ConcurrentBag<INamedTypeSymbol> stateTypes,
        ConcurrentBag<UpcasterEdge> upcasters)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.IsActorInterface())
        {
            AnalyzeActorInterfaceReturns(context, type);
            AnalyzeWireBaseline(context, baseline, type);
        }

        AnalyzeStateBaseline(context, baseline, type);
        AnalyzeMigrationFingerprintBaseline(context, baseline, type);
        AnalyzeFilter(context, type);
        CollectActorImplementation(type, actorImplementations);
        CollectStateType(type, stateTypes);
        CollectUpcaster(type, upcasters);
    }

    private static void CollectActorImplementation(INamedTypeSymbol type, ConcurrentBag<ActorImplementationInfo> actorImplementations)
    {
        if (type.IsAbstract || !type.HasAttribute("Dapr.Actors.Next.Abstractions.Attributes.DaprActorAttribute"))
        {
            return;
        }

        var actorInterfaces = type.AllInterfaces
            .Where(IsConcreteActorInterface)
            .Select(actorInterface => actorInterface.OriginalDefinition)
            .GroupBy(actorInterface => actorInterface.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), StringComparer.Ordinal)
            .Select(group => group.First())
            .ToImmutableArray();
        if (actorInterfaces.Length == 0)
        {
            return;
        }

        actorImplementations.Add(new ActorImplementationInfo(type, GetDaprActorTypeName(type), actorInterfaces, type.Locations.FirstOrDefault()));
    }

    private static bool IsConcreteActorInterface(INamedTypeSymbol type) =>
        type.ToDisplayString() != "Dapr.Actors.Next.Abstractions.IActor" &&
        type.Implements("Dapr.Actors.Next.Abstractions.IActor");

    private static string GetDaprActorTypeName(INamedTypeSymbol type)
    {
        foreach (var attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != "Dapr.Actors.Next.Abstractions.Attributes.DaprActorAttribute")
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 1 &&
                attribute.ConstructorArguments[0].Value is string value &&
                !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return type.Name;
    }

    private static void AnalyzeDuplicateActorTypeNames(
        CompilationAnalysisContext context,
        ConcurrentBag<ActorImplementationInfo> actorImplementations,
        ConcurrentBag<ExplicitActorName> explicitActorNames)
    {
        var actors = actorImplementations.ToArray();
        if (actors.Length < 2)
        {
            return;
        }

        var aliases = explicitActorNames.ToArray();
        var effectiveActors = actors
            .Select(actor => actor with { ActorTypeName = GetEffectiveActorTypeName(actor.Type, actor.ActorTypeName, aliases) })
            .ToArray();

        foreach (var group in effectiveActors.GroupBy(actor => actor.ActorTypeName, StringComparer.Ordinal))
        {
            var duplicateActors = group.ToArray();
            if (duplicateActors.Length < 2)
            {
                continue;
            }

            foreach (var sharedInterface in SharedActorInterfaces(duplicateActors))
            {
                foreach (var actor in duplicateActors.Where(item => item.Interfaces.Any(actorInterface => SymbolEqualityComparer.Default.Equals(actorInterface, sharedInterface))))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        ActorAnalyzerDiagnostics.DuplicateActorTypeName,
                        actor.Location,
                        actor.ActorTypeName,
                        sharedInterface.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                }
            }
        }
    }

    private static string GetEffectiveActorTypeName(INamedTypeSymbol actorType, string attributeActorTypeName, ExplicitActorName[] aliases)
    {
        var actorAliases = aliases
            .Where(alias => SymbolEqualityComparer.Default.Equals(alias.ActorType, actorType))
            .Select(alias => alias.ActorTypeName)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return actorAliases.Length == 1 ? actorAliases[0] : attributeActorTypeName;
    }

    private static IEnumerable<INamedTypeSymbol> SharedActorInterfaces(IReadOnlyList<ActorImplementationInfo> actors)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var actor in actors)
        {
            foreach (var actorInterface in actor.Interfaces)
            {
                var name = actorInterface.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
                if (!seen.Add(name))
                {
                    yield return actorInterface;
                }
            }
        }
    }

    private static void AnalyzeActorInterfaceReturns(SymbolAnalysisContext context, INamedTypeSymbol type)
    {
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>().Where(static m => m.MethodKind == MethodKind.Ordinary))
        {
            if (!method.ReturnType.IsSupportedActorReturnType())
            {
                context.ReportDiagnostic(Diagnostic.Create(ActorAnalyzerDiagnostics.InvalidActorMethodReturnType, method.Locations.FirstOrDefault(), method.Name));
            }
        }
    }

    private static void AnalyzeStateBaseline(SymbolAnalysisContext context, ActorBaseline baseline, INamedTypeSymbol type)
    {
        var current = BaselineEntry.ForState(type);
        if (!baseline.Shipped.TryGetValue(current.Key, out var shipped))
        {
            return;
        }

        var breakReason = FindBreakingMemberChange(shipped, current);
        if (breakReason is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            ActorAnalyzerDiagnostics.StateShapeChanged,
            type.Locations.FirstOrDefault(),
            properties: ImmutableDictionary<string, string?>.Empty
                .Add("baseline.kind", current.Kind)
                .Add("baseline.name", current.Name)
                .Add("baseline.current", current.ToBaselineLine()),
            type.BaselineName(),
            breakReason));
    }

    private static bool HasBumpedContractVersion(INamespaceSymbol namespaceSymbol, INamedTypeSymbol actorInterface, string baselineVersion)
    {
        var shippedVersion = ParseVersion(baselineVersion);
        foreach (var type in EnumerateNamespaceTypes(namespaceSymbol))
        {
            if (!type.IsActorImplementation() ||
                !type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, actorInterface)))
            {
                continue;
            }

            foreach (var attribute in type.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() != "Dapr.Actors.Next.Abstractions.Attributes.DaprActorAttribute")
                {
                    continue;
                }

                foreach (var namedArgument in attribute.NamedArguments)
                {
                    if (namedArgument is { Key: "ContractVersion", Value.Value: int currentVersion } &&
                        currentVersion > shippedVersion)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static int ParseVersion(string version)
    {
        const string prefix = "v=";
        return version.StartsWith(prefix, StringComparison.Ordinal) &&
            int.TryParse(version.Substring(prefix.Length), out var parsed)
            ? parsed
            : 1;
    }

    private static void AnalyzeWireBaseline(SymbolAnalysisContext context, ActorBaseline baseline, INamedTypeSymbol type)
    {
        var current = BaselineEntry.ForInterface(type);
        if (!baseline.Shipped.TryGetValue(current.Key, out var shipped))
        {
            return;
        }

        if (HasBumpedContractVersion(context.Compilation.GlobalNamespace, type, shipped.Version))
        {
            return;
        }

        var breakReason = FindBreakingMemberChange(shipped, current);
        if (breakReason is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            ActorAnalyzerDiagnostics.WireContractChanged,
            type.Locations.FirstOrDefault(),
            properties: ImmutableDictionary<string, string?>.Empty
                .Add("baseline.kind", current.Kind)
                .Add("baseline.name", current.Name)
                .Add("baseline.current", current.ToBaselineLine()),
            type.BaselineName(),
            breakReason));
    }

    private static string? FindBreakingMemberChange(BaselineEntry shipped, BaselineEntry current)
    {
        foreach (var shippedMember in shipped.Members)
        {
            if (!current.Members.TryGetValue(shippedMember.Key, out var currentValue))
            {
                return "member '" + shippedMember.Key + "' was removed";
            }

            if (!StringComparer.Ordinal.Equals(shippedMember.Value, currentValue))
            {
                return "member '" + shippedMember.Key + "' changed from '" + shippedMember.Value + "' to '" + currentValue + "'";
            }
        }

        return null;
    }

    private static void AnalyzeFilter(SymbolAnalysisContext context, INamedTypeSymbol type)
    {
        if (!type.Implements("Dapr.Actors.Next.Abstractions.Filters.IActorTurnFilter"))
        {
            return;
        }

        foreach (var syntaxReference in type.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax(context.CancellationToken) is not ClassDeclarationSyntax classDeclaration)
            {
                continue;
            }

            foreach (var method in classDeclaration.Members.OfType<MethodDeclarationSyntax>().Where(static m => m.Identifier.Text == "InvokeAsync"))
            {
                var body = method.Body;
                if (body is null)
                {
                    continue;
                }

                foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    var text = invocation.Expression.ToString();
                    if (text.EndsWith("next", StringComparison.Ordinal) ||
                        text.EndsWith("TryGetValue", StringComparison.Ordinal) ||
                        text.EndsWith("TryGetTenant", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    context.ReportDiagnostic(Diagnostic.Create(ActorAnalyzerDiagnostics.BusinessLogicInFilter, invocation.GetLocation(), text));
                    return;
                }
            }
        }
    }

    private static void AnalyzeField(SymbolAnalysisContext context)
    {
        var field = (IFieldSymbol)context.Symbol;
        if (field.IsStatic || field.IsConst || field.IsReadOnly)
        {
            return;
        }

        if (field.ContainingType is not { } containingType || !containingType.IsActorImplementation())
        {
            return;
        }

        if (IsObviouslyTurnSafeField(field.Type))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ActorAnalyzerDiagnostics.MutableActorField, field.Locations.FirstOrDefault(), field.Name));
    }

    private static bool IsObviouslyTurnSafeField(ITypeSymbol type)
    {
        if (type.IsValueType || type.SpecialType == SpecialType.System_String)
        {
            return true;
        }

        var name = type.ToDisplayString();
        return name == "System.TimeProvider" ||
            name == "System.IServiceProvider" ||
            name.Contains("Microsoft.Extensions.Logging.ILogger", StringComparison.Ordinal) ||
            name.EndsWith("Client", StringComparison.Ordinal) ||
            name.EndsWith("Logger", StringComparison.Ordinal);
    }

    private static void CollectStateUsage(OperationAnalysisContext context, ConcurrentBag<StateUsage> stateUsages)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (method.TypeArguments.Length != 1 ||
            method.Name is not ("TryGetAsync" or "GetOrCreateAsync" or "SetAsync" or "GraduateAsync"))
        {
            return;
        }

        var containingType = method.ContainingType?.ToDisplayString();
        if (containingType != "Dapr.Actors.Next.Abstractions.State.IActorStateAccessor")
        {
            return;
        }

        var stateName = invocation.Arguments.Length > 0 &&
            invocation.Arguments[0].Value.ConstantValue is { HasValue: true, Value: string value }
                ? value
                : null;
        stateUsages.Add(new StateUsage(method.TypeArguments[0], stateName, invocation.Syntax.GetLocation()));
    }

    private static void CollectStateType(INamedTypeSymbol type, ConcurrentBag<INamedTypeSymbol> stateTypes)
    {
        if (type.TypeParameters.Length != 0 || type.IsAbstract || type.TypeKind is not (TypeKind.Class or TypeKind.Struct) ||
            type.IsActorInterface() || type.Implements("Dapr.Actors.Next.Abstractions.State.IActorStateUpcaster<TFromType, TToType>") ||
            !ActorStateMigrationShared.TryParseNumericVersion(type.Name, out _, out _))
        {
            return;
        }

        stateTypes.Add(type);
    }

    private static void CollectUpcaster(INamedTypeSymbol type, ConcurrentBag<UpcasterEdge> upcasters)
    {
        foreach (var implemented in type.AllInterfaces)
        {
            if (!IsUpcasterInterface(implemented) ||
                implemented.TypeArguments.Length != 2 ||
                implemented.TypeArguments[0] is not INamedTypeSymbol from ||
                implemented.TypeArguments[1] is not INamedTypeSymbol to)
            {
                continue;
            }

            upcasters.Add(new UpcasterEdge(type, from, to, type.Locations.FirstOrDefault()));
        }
    }

    private static void AnalyzeMigrationFingerprintBaseline(SymbolAnalysisContext context, ActorBaseline baseline, INamedTypeSymbol type)
    {
        BaselineEntry? current = null;
        if (type.Implements("Dapr.Actors.Next.Abstractions.State.IActorStateUpcaster<TFromType, TToType>"))
        {
            current = BaselineEntry.ForMigrationUpcaster(type);
        }
        else if (ActorStateMigrationShared.TryParseNumericVersion(type.Name, out _, out _))
        {
            current = BaselineEntry.ForMigrationState(type);
        }

        if (current is null || !baseline.Shipped.TryGetValue(current.Key, out var shipped))
        {
            return;
        }

        var breakReason = FindBreakingMemberChange(shipped, current);
        if (breakReason is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            ActorAnalyzerDiagnostics.StateShapeChanged,
            type.Locations.FirstOrDefault(),
            properties: ImmutableDictionary<string, string?>.Empty
                .Add("baseline.kind", current.Kind)
                .Add("baseline.name", current.Name)
                .Add("baseline.current", current.ToBaselineLine()),
            type.BaselineName(),
            "you modified a type/upcaster that has already participated in migration - this risks corrupting persisted state"));
    }

    private static void AnalyzeStateMigrationGraph(
        CompilationAnalysisContext context,
        ConcurrentBag<INamedTypeSymbol> stateTypes,
        ConcurrentBag<UpcasterEdge> upcasters,
        ConcurrentBag<StateUsage> stateUsages)
    {
        var usages = stateUsages.ToArray();
        var edges = upcasters.ToArray();
        var relevantTypeNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var usage in usages)
        {
            relevantTypeNames.Add(usage.Type.BaselineName());
        }

        foreach (var edge in edges)
        {
            relevantTypeNames.Add(edge.From.BaselineName());
            relevantTypeNames.Add(edge.To.BaselineName());
        }

        var nodes = stateTypes
            .Select(BuildStateNode)
            .Where(node => node is not null)
            .Cast<StateNode>()
            .ToDictionary(node => node.Type.BaselineName(), StringComparer.Ordinal);

        foreach (var group in nodes.Values.GroupBy(static node => node.CanonicalName, StringComparer.Ordinal))
        {
            if (group.Count(node => relevantTypeNames.Contains(node.Type.BaselineName())) > 0)
            {
                foreach (var node in group)
                {
                    relevantTypeNames.Add(node.Type.BaselineName());
                }
            }
        }

        var relevantNodes = nodes.Values
            .Where(node => relevantTypeNames.Contains(node.Type.BaselineName()))
            .OrderBy(static node => node.CanonicalName, StringComparer.Ordinal)
            .ThenBy(static node => node.Version)
            .ThenBy(static node => node.Type.BaselineName(), StringComparer.Ordinal)
            .ToArray();
        if (relevantNodes.Length == 0)
        {
            return;
        }

        var edgeKeys = edges
            .Select(edge => (From: edge.From.BaselineName(), To: edge.To.BaselineName(), edge.Location))
            .ToArray();
        var reportedMissing = new HashSet<string>(StringComparer.Ordinal);

        foreach (var usage in usages)
        {
            if (!nodes.TryGetValue(usage.Type.BaselineName(), out var target))
            {
                continue;
            }

            var predecessor = FindPredecessor(target, nodes.Values);
            if (predecessor is null || HasIncomingEdge(target, edgeKeys) || IsAdditive(predecessor, target))
            {
                continue;
            }

            reportedMissing.Add(target.Type.BaselineName());
            context.ReportDiagnostic(Diagnostic.Create(
                ActorAnalyzerDiagnostics.BrokenUpcasterChain,
                usage.Location,
                properties: UpcasterProperties(predecessor.Type.BaselineName(), target.Type.BaselineName(), predecessor, target),
                target.Type.Name,
                predecessor.Type.Name));
        }

        foreach (var family in BuildConnectedFamilies(relevantNodes, edgeKeys))
        {
            AnalyzeNonUniquePaths(context, family, edgeKeys);
        }

        foreach (var familyGroup in relevantNodes.GroupBy(static node => node.CanonicalName, StringComparer.Ordinal))
        {
            var familyNodes = familyGroup.OrderBy(static node => node.Version).ThenBy(static node => node.Type.BaselineName(), StringComparer.Ordinal).ToArray();
            for (var i = 0; i < familyNodes.Length - 1; i++)
            {
                var from = familyNodes[i];
                var to = familyNodes[i + 1];
                if (HasEdge(from, to, edgeKeys) || IsAdditive(from, to))
                {
                    continue;
                }

                var descriptor = IsExplicitFragmentBoundary(from, to, edgeKeys)
                    ? ActorAnalyzerDiagnostics.UpcasterChainGap
                    : ActorAnalyzerDiagnostics.NonAdditiveMigrationStep;
                context.ReportDiagnostic(Diagnostic.Create(
                    descriptor,
                    to.Type.Locations.FirstOrDefault(),
                    properties: UpcasterProperties(from.Type.BaselineName(), to.Type.BaselineName(), from, to),
                    descriptor == ActorAnalyzerDiagnostics.UpcasterChainGap ? from.CanonicalName : from.Type.Name,
                    descriptor == ActorAnalyzerDiagnostics.UpcasterChainGap ? from.Type.Name : to.Type.Name,
                    to.Type.Name));
            }

            foreach (var usage in usages)
            {
                if (!nodes.TryGetValue(usage.Type.BaselineName(), out var used) ||
                    !StringComparer.Ordinal.Equals(used.CanonicalName, familyGroup.Key) ||
                    reportedMissing.Contains(used.Type.BaselineName()) ||
                    familyNodes.Length < 2 ||
                    HasIncomingEdge(used, edgeKeys) ||
                    HasOutgoingEdge(used, edgeKeys) ||
                    HasAdditiveAdjacency(used, familyNodes))
                {
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    ActorAnalyzerDiagnostics.UnconnectedStateFamilyMember,
                    usage.Location,
                    properties: ImmutableDictionary<string, string?>.Empty
                        .Add("upcaster.from", FindPredecessor(used, familyNodes)?.Type.BaselineName() ?? used.Type.BaselineName())
                        .Add("upcaster.to", used.Type.BaselineName()),
                    used.Type.Name,
                    familyGroup.Key));
            }
        }

        foreach (var nameGroup in usages.Where(static usage => !string.IsNullOrWhiteSpace(usage.StateName)).GroupBy(static usage => usage.StateName!, StringComparer.Ordinal))
        {
            var families = nameGroup
                .Select(usage => nodes.TryGetValue(usage.Type.BaselineName(), out var node) ? node.CanonicalName : usage.Type.BaselineName())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static family => family, StringComparer.Ordinal)
                .ToArray();
            if (families.Length < 2)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                ActorAnalyzerDiagnostics.MultipleFamiliesForStateName,
                nameGroup.OrderBy(static usage => usage.Location.SourceSpan.Start).First().Location,
                nameGroup.Key,
                string.Join(", ", families)));
        }
    }

    private static StateNode? BuildStateNode(INamedTypeSymbol type)
    {
        if (!ActorStateMigrationShared.TryParseNumericVersion(type.Name, out var simpleCanonicalName, out var versionText) ||
            !long.TryParse(versionText, out var version))
        {
            return null;
        }

        var canonicalName = type.ContainingNamespace.IsGlobalNamespace
            ? simpleCanonicalName
            : type.ContainingNamespace.ToDisplayString() + "." + simpleCanonicalName;
        return new StateNode(
            type,
            canonicalName,
            version,
            ActorStateMigrationShared.GetSerializableMembers(type),
            ActorStateMigrationShared.HasPublicParameterlessConstructor(type));
    }

    private static ImmutableDictionary<string, string?> UpcasterProperties(string from, string to, StateNode? fromNode = null, StateNode? toNode = null)
    {
        var properties = ImmutableDictionary<string, string?>.Empty
            .Add("upcaster.from", from)
            .Add("upcaster.to", to);

        if (fromNode is not null && toNode is not null)
        {
            var copied = SharedWritableMembers(fromNode, toNode);
            if (copied.Length > 0)
            {
                properties = properties.Add("upcaster.copiedMembers", string.Join(";", copied));
            }
        }

        return properties;
    }

    private static string[] SharedWritableMembers(StateNode from, StateNode to)
    {
        var fromMembers = from.Members.ToDictionary(static member => member.Name, StringComparer.Ordinal);
        return to.Members
            .Where(member => member.CanWrite &&
                fromMembers.TryGetValue(member.Name, out var fromMember) &&
                StringComparer.Ordinal.Equals(fromMember.TypeName, member.TypeName))
            .Select(static member => member.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private static StateNode? FindPredecessor(StateNode target, IEnumerable<StateNode> nodes)
    {
        var sameFamily = nodes
            .Where(node => StringComparer.Ordinal.Equals(node.CanonicalName, target.CanonicalName) && node.Version < target.Version)
            .OrderByDescending(static node => node.Version)
            .ThenBy(static node => node.Type.BaselineName(), StringComparer.Ordinal)
            .FirstOrDefault();
        if (sameFamily is not null)
        {
            return sameFamily;
        }

        var previousVersion = target.Version - 1;
        var candidates = nodes
            .Where(node => node.Version == previousVersion)
            .OrderBy(static node => node.Type.BaselineName(), StringComparer.Ordinal)
            .ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static bool IsAdditive(StateNode from, StateNode to) =>
        ActorStateMigrationShared.IsAdditiveStep(from.Members, to.Members, to.HasPublicParameterlessConstructor, out _);

    private static bool HasEdge(StateNode from, StateNode to, IEnumerable<(string From, string To, Location? Location)> edgeKeys) =>
        edgeKeys.Any(edge => StringComparer.Ordinal.Equals(edge.From, from.Type.BaselineName()) && StringComparer.Ordinal.Equals(edge.To, to.Type.BaselineName()));

    private static bool HasIncomingEdge(StateNode node, IEnumerable<(string From, string To, Location? Location)> edgeKeys) =>
        edgeKeys.Any(edge => StringComparer.Ordinal.Equals(edge.To, node.Type.BaselineName()));

    private static bool HasOutgoingEdge(StateNode node, IEnumerable<(string From, string To, Location? Location)> edgeKeys) =>
        edgeKeys.Any(edge => StringComparer.Ordinal.Equals(edge.From, node.Type.BaselineName()));

    private static bool HasAdditiveAdjacency(StateNode node, IReadOnlyList<StateNode> familyNodes)
    {
        for (var i = 0; i < familyNodes.Count; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(familyNodes[i].Type, node.Type))
            {
                continue;
            }

            return i > 0 && IsAdditive(familyNodes[i - 1], node) ||
                   i < familyNodes.Count - 1 && IsAdditive(node, familyNodes[i + 1]);
        }

        return false;
    }

    private static bool IsExplicitFragmentBoundary(StateNode from, StateNode to, IEnumerable<(string From, string To, Location? Location)> edgeKeys) =>
        HasIncomingEdge(from, edgeKeys) && HasOutgoingEdge(to, edgeKeys);

    private static IEnumerable<StateNode[]> BuildConnectedFamilies(
        IReadOnlyCollection<StateNode> nodes,
        IReadOnlyCollection<(string From, string To, Location? Location)> edgeKeys)
    {
        var byName = nodes.ToDictionary(static node => node.Type.BaselineName(), StringComparer.Ordinal);
        var adjacency = nodes.ToDictionary(static node => node.Type.BaselineName(), static _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);

        foreach (var group in nodes.GroupBy(static node => node.CanonicalName, StringComparer.Ordinal))
        {
            var ordered = group.OrderBy(static node => node.Version).ThenBy(static node => node.Type.BaselineName(), StringComparer.Ordinal).ToArray();
            for (var i = 0; i < ordered.Length - 1; i++)
            {
                Connect(ordered[i].Type.BaselineName(), ordered[i + 1].Type.BaselineName());
            }
        }

        foreach (var edge in edgeKeys)
        {
            Connect(edge.From, edge.To);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            var name = node.Type.BaselineName();
            if (!seen.Add(name))
            {
                continue;
            }

            var component = new List<StateNode>();
            var stack = new Stack<string>();
            stack.Push(name);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                component.Add(byName[current]);
                foreach (var next in adjacency[current])
                {
                    if (seen.Add(next))
                    {
                        stack.Push(next);
                    }
                }
            }

            yield return component.OrderBy(static item => item.Version).ThenBy(static item => item.Type.BaselineName(), StringComparer.Ordinal).ToArray();
        }

        void Connect(string left, string right)
        {
            if (!adjacency.ContainsKey(left) || !adjacency.ContainsKey(right))
            {
                return;
            }

            adjacency[left].Add(right);
            adjacency[right].Add(left);
        }
    }

    private static void AnalyzeNonUniquePaths(
        CompilationAnalysisContext context,
        IReadOnlyList<StateNode> family,
        IReadOnlyCollection<(string From, string To, Location? Location)> explicitEdges)
    {
        var directedEdges = new List<(string From, string To)>();
        for (var i = 0; i < family.Count - 1; i++)
        {
            var from = family[i].Type.BaselineName();
            var to = family[i + 1].Type.BaselineName();
            if (IsAdditive(family[i], family[i + 1]) &&
                !explicitEdges.Any(edge => StringComparer.Ordinal.Equals(edge.From, from) && StringComparer.Ordinal.Equals(edge.To, to)))
            {
                directedEdges.Add((from, to));
            }
        }

        directedEdges.AddRange(explicitEdges.Select(static edge => (edge.From, edge.To)));
        foreach (var target in family)
        {
            var hasMultiplePaths = family.Any(source => CountPaths(source.Type.BaselineName(), target.Type.BaselineName(), directedEdges, new HashSet<string>(StringComparer.Ordinal)) > 1);
            if (!hasMultiplePaths)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                ActorAnalyzerDiagnostics.NonUniqueFoldPath,
                target.Type.Locations.FirstOrDefault(),
                family[0].CanonicalName,
                target.Type.Name));
            return;
        }
    }

    private static int CountPaths(string current, string target, IReadOnlyCollection<(string From, string To)> edges, HashSet<string> seen)
    {
        if (StringComparer.Ordinal.Equals(current, target))
        {
            return 1;
        }

        if (!seen.Add(current))
        {
            return 0;
        }

        var count = 0;
        foreach (var edge in edges.Where(edge => StringComparer.Ordinal.Equals(edge.From, current)))
        {
            count += CountPaths(edge.To, target, edges, seen);
            if (count > 1)
            {
                break;
            }
        }

        seen.Remove(current);
        return count;
    }

    private static bool IsUpcasterInterface(INamedTypeSymbol type) =>
        type.OriginalDefinition.ToDisplayString() == "Dapr.Actors.Next.Abstractions.State.IActorStateUpcaster<TFromType, TToType>";

    private static IEnumerable<INamedTypeSymbol> EnumerateNamespaceTypes(INamespaceSymbol namespaceSymbol)
    {
        foreach (var member in namespaceSymbol.GetTypeMembers())
        {
            yield return member;
        }

        foreach (var childNamespace in namespaceSymbol.GetNamespaceMembers())
        {
            foreach (var member in EnumerateNamespaceTypes(childNamespace))
            {
                yield return member;
            }
        }
    }

    private static bool IsInsideActorTurn(SemanticModel? semanticModel, SyntaxNode syntax, CancellationToken cancellationToken)
    {
        if (semanticModel is null)
        {
            return false;
        }

        var typeDeclaration = syntax.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (typeDeclaration is null ||
            semanticModel.GetDeclaredSymbol(typeDeclaration, cancellationToken) is not { } type)
        {
            return false;
        }

        return type.IsActorImplementation() ||
            type.Implements("Dapr.Actors.Next.Abstractions.State.IActorStateUpcaster<TFromType, TToType>");
    }

    private static bool IsTaskLike(ITypeSymbol? type)
    {
        if (type is null)
        {
            return false;
        }

        var original = type.OriginalDefinition.ToDisplayString();
        return original is "System.Threading.Tasks.Task" or
            "System.Threading.Tasks.Task<TResult>" or
            "System.Threading.Tasks.ValueTask" or
            "System.Threading.Tasks.ValueTask<TResult>";
    }

    private static bool IsBlockingWaitType(ITypeSymbol type)
    {
        var name = type.OriginalDefinition.ToDisplayString();
        return name is "System.Threading.Tasks.Task" or
            "System.Threading.Tasks.Task<TResult>" or
            "System.Threading.Tasks.ValueTask" or
            "System.Threading.Tasks.ValueTask<TResult>" or
            "System.Threading.WaitHandle";
    }

    private sealed record ActorImplementationInfo(
        INamedTypeSymbol Type,
        string ActorTypeName,
        ImmutableArray<INamedTypeSymbol> Interfaces,
        Location? Location);

    private sealed record ExplicitActorName(INamedTypeSymbol ActorType, string ActorTypeName);

    private sealed record StateUsage(ITypeSymbol Type, string? StateName, Location Location);

    private sealed record UpcasterEdge(INamedTypeSymbol Implementation, INamedTypeSymbol From, INamedTypeSymbol To, Location? Location);

    private sealed record StateNode(
        INamedTypeSymbol Type,
        string CanonicalName,
        long Version,
        ImmutableArray<ActorStateMigrationShared.StateMember> Members,
        bool HasPublicParameterlessConstructor);
}
