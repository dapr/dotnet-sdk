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
public class ActorRegistrationAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor DiagnosticDescriptor = new(
        "DAPR0001",
        "Actor class not registered",
        "The actor class '{0}' is not registered",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Gets the supported diagnostics for this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(DiagnosticDescriptor);

    /// <summary>
    /// Initializes the analyzer.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeActorRegistration, SyntaxKind.ClassDeclaration);
    }

    /// <summary>
    /// Analyzes the actor registration.
    /// </summary>
    /// <param name="context">The syntax node analysis context.</param>
    private void AnalyzeActorRegistration(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        
        if (classDeclaration.BaseList != null)
        {
            var baseTypeSyntax = classDeclaration.BaseList.Types[0].Type;

            if (context.SemanticModel.GetSymbolInfo(baseTypeSyntax).Symbol is INamedTypeSymbol baseTypeSymbol)
            {
                var baseTypeName = baseTypeSymbol.ToDisplayString();
                if (baseTypeName == "Dapr.Actors.Runtime.Actor" || baseTypeName == "Actor")
                {
                    var actorTypeName = classDeclaration.Identifier.Text;
                    bool isRegistered = CheckIfActorIsRegistered(actorTypeName, context.SemanticModel);
                    if (!isRegistered)
                    {
                        var diagnostic = Diagnostic.Create(DiagnosticDescriptor, classDeclaration.Identifier.GetLocation(), actorTypeName);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Checks if the actor is registered.
    /// </summary>
    /// <param name="actorTypeName">The name of the actor type.</param>
    /// <param name="semanticModel">The semantic model.</param>
    /// <returns>True if the actor is registered, otherwise false.</returns>
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
}
