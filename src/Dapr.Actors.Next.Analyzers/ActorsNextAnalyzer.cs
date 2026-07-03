using System.Collections.Immutable;
using System.Text.RegularExpressions;
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
    private static readonly Regex VersionSuffix = new("^(?<root>.+)V(?<version>[0-9]+)$", RegexOptions.Compiled);

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
        ActorAnalyzerDiagnostics.MutableActorField);

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
            startContext.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
            startContext.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
            startContext.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
            startContext.RegisterSymbolAction(ctx => AnalyzeNamedType(ctx, baseline), SymbolKind.NamedType);
            startContext.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
            startContext.RegisterSymbolAction(AnalyzeUpcasterChain, SymbolKind.NamedType);
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

    private static void AnalyzeNamedType(SymbolAnalysisContext context, ActorBaseline baseline)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.IsActorInterface())
        {
            AnalyzeActorInterfaceReturns(context, type);
            AnalyzeWireBaseline(context, baseline, type);
        }

        AnalyzeStateBaseline(context, baseline, type);
        AnalyzeFilter(context, type);
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

    private static void AnalyzeUpcasterChain(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        foreach (var implemented in type.AllInterfaces)
        {
            if (implemented.OriginalDefinition.ToDisplayString() != "Dapr.Actors.Next.Abstractions.State.IActorStateUpcaster<TFromType, TToType>" ||
                implemented.TypeArguments.Length != 2)
            {
                continue;
            }

            if (TryGetVersion(implemented.TypeArguments[0], out var fromRoot, out var fromVersion) &&
                TryGetVersion(implemented.TypeArguments[1], out var toRoot, out var toVersion) &&
                StringComparer.Ordinal.Equals(fromRoot, toRoot) &&
                toVersion == fromVersion + 1 &&
                fromVersion > 1)
            {
                var requiredFrom = fromRoot + "V" + (fromVersion - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                var requiredTo = fromRoot + "V" + fromVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (!HasUpcasterHop(context.Compilation.GlobalNamespace, requiredFrom, requiredTo))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        ActorAnalyzerDiagnostics.BrokenUpcasterChain,
                        type.Locations.FirstOrDefault(),
                        properties: ImmutableDictionary<string, string?>.Empty
                            .Add("upcaster.from", requiredFrom)
                            .Add("upcaster.to", requiredTo),
                        requiredFrom,
                        requiredTo));
                }
            }
        }
    }

    private static bool HasUpcasterHop(INamespaceSymbol namespaceSymbol, string fromType, string toType)
    {
        foreach (var type in EnumerateNamespaceTypes(namespaceSymbol))
        {
            foreach (var implemented in type.AllInterfaces)
            {
                if (implemented.OriginalDefinition.ToDisplayString() == "Dapr.Actors.Next.Abstractions.State.IActorStateUpcaster<TFromType, TToType>" &&
                    implemented.TypeArguments.Length == 2 &&
                    implemented.TypeArguments[0].BaselineName() == fromType &&
                    implemented.TypeArguments[1].BaselineName() == toType)
                {
                    return true;
                }
            }
        }

        return false;
    }

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

    private static bool TryGetVersion(ITypeSymbol type, out string root, out int version)
    {
        var match = VersionSuffix.Match(type.Name);
        if (!match.Success)
        {
            root = string.Empty;
            version = 0;
            return false;
        }

        root = type.ContainingNamespace.IsGlobalNamespace
            ? match.Groups["root"].Value
            : type.ContainingNamespace.ToDisplayString() + "." + match.Groups["root"].Value;
        version = int.Parse(match.Groups["version"].Value, System.Globalization.CultureInfo.InvariantCulture);
        return true;
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

        return type.IsActorImplementation();
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
}
