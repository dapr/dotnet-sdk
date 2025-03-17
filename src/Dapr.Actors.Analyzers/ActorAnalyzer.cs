using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dapr.Actors.Analyzers;

/// <summary>
/// Analyzes actor registration in Dapr applications.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ActorAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor DiagnosticDescriptorActorRegistration = new(
        "DAPR0001",
        "Actor class not registered",
        "The actor class '{0}' is not registered",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DiagnosticDescriptorJsonSerialization = new(
        "DAPR0002",
        "Use JsonSerialization",
        "Add options.UseJsonSerialization to support interoperability with non-.NET actors",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DiagnosticDescriptorMapActorsHandlers = new(
        "DAPR0003",
        "Call MapActorsHandlers",
        "Call app.MapActorsHandlers to map endpoints for Dapr actors",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Gets the supported diagnostics for this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        DiagnosticDescriptorActorRegistration,
        DiagnosticDescriptorJsonSerialization,
        DiagnosticDescriptorMapActorsHandlers);

    /// <summary>
    /// Initializes the analyzer.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeActorRegistration, SyntaxKind.ClassDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeSerialization, SyntaxKind.CompilationUnit);
        context.RegisterSyntaxNodeAction(AnalyzeMapActorsHandlers, SyntaxKind.CompilationUnit);
    }

    private void AnalyzeActorRegistration(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        if (classDeclaration.BaseList != null)
        {
            var baseTypeSyntax = classDeclaration.BaseList.Types[0].Type;

            if (context.SemanticModel.GetSymbolInfo(baseTypeSyntax).Symbol is INamedTypeSymbol baseTypeSymbol)
            {
                var baseTypeName = baseTypeSymbol.ToDisplayString();

                {
                    var actorTypeName = classDeclaration.Identifier.Text;
                    bool isRegistered = CheckIfActorIsRegistered(actorTypeName, context.SemanticModel);
                    if (!isRegistered)
                    {
                        var diagnostic = Diagnostic.Create(DiagnosticDescriptorActorRegistration, classDeclaration.Identifier.GetLocation(), actorTypeName);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }

    private static bool CheckIfActorIsRegistered(string actorTypeName, SemanticModel semanticModel)
    {
        var methodInvocations = new List<InvocationExpressionSyntax>();
        foreach (var syntaxTree in semanticModel.Compilation.SyntaxTrees)
        {
            var root = syntaxTree.GetRoot();
            methodInvocations.AddRange(root.DescendantNodes().OfType<InvocationExpressionSyntax>());
        }

        foreach (var invocation in methodInvocations)
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                continue;
            }

            var methodName = memberAccess.Name.Identifier.Text;
            if (methodName == "RegisterActor")
            {
                if (memberAccess.Name is GenericNameSyntax typeArgumentList && typeArgumentList.TypeArgumentList.Arguments.Count > 0)
                {
                    if (typeArgumentList.TypeArgumentList.Arguments[0] is IdentifierNameSyntax typeArgument)
                    {
                        if (typeArgument.Identifier.Text == actorTypeName)
                        {
                            return true;
                        }
                    }
                    else if (typeArgumentList.TypeArgumentList.Arguments[0] is QualifiedNameSyntax qualifiedName)
                    {
                        if (qualifiedName.Right.Identifier.Text == actorTypeName)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    private void AnalyzeSerialization(SyntaxNodeAnalysisContext context)
    {
        var addActorsInvocation = FindInvocation(context, "AddActors");

        if (addActorsInvocation != null)
        {
            var optionsLambda = addActorsInvocation.ArgumentList.Arguments
                .Select(arg => arg.Expression)
                .OfType<SimpleLambdaExpressionSyntax>()
                .FirstOrDefault();

            if (optionsLambda != null)
            {
                var lambdaBody = optionsLambda.Body;
                var assignments = lambdaBody.DescendantNodes().OfType<AssignmentExpressionSyntax>();

                var useJsonSerialization = assignments.Any(assignment =>
                    assignment.Left is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Name is IdentifierNameSyntax identifier &&
                    identifier.Identifier.Text == "UseJsonSerialization" &&
                    assignment.Right is LiteralExpressionSyntax literal &&
                    literal.Token.ValueText == "true");

                if (!useJsonSerialization)
                {
                    var diagnostic = Diagnostic.Create(DiagnosticDescriptorJsonSerialization, addActorsInvocation.GetLocation());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    private InvocationExpressionSyntax? FindInvocation(SyntaxNodeAnalysisContext context, string methodName)
    {
        foreach (var syntaxTree in context.SemanticModel.Compilation.SyntaxTrees)
        {
            var root = syntaxTree.GetRoot();
            var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Name.Identifier.Text == methodName);

            if (invocation != null)
            {
                return invocation;
            }
        }

        return null;
    }

    private void AnalyzeMapActorsHandlers(SyntaxNodeAnalysisContext context)
    {
        var addActorsInvocation = FindInvocation(context, "AddActors");

        if (addActorsInvocation != null)
        {            
            bool invokedByWebApplication = false;
            var mapActorsHandlersInvocation = FindInvocation(context, "MapActorsHandlers");

            if (mapActorsHandlersInvocation?.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess.Expression);
                if (symbolInfo.Symbol is ILocalSymbol localSymbol)
                {
                    var type = localSymbol.Type;
                    if (type.ToDisplayString() == "Microsoft.AspNetCore.Builder.WebApplication")
                    {
                        invokedByWebApplication = true;
                    }
                }
            }

            if (mapActorsHandlersInvocation == null || !invokedByWebApplication)
            {
                var diagnostic = Diagnostic.Create(DiagnosticDescriptorMapActorsHandlers, addActorsInvocation.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
