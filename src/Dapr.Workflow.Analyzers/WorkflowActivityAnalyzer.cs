using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dapr.Workflow.Analyzers;

/// <summary>
/// Analyzes whether or not workflow activities are registered.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class WorkflowActivityAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor DiagnosticDescriptor = new(
        "DAPR1001",
        "Class not registered in DI",
        "The class '{0}' is not registered in the DI container",
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
        context.RegisterSyntaxNodeAction(AnalyzeSyntaxNode, SyntaxKind.InvocationExpression);
    }

    private void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
    {
        var invocationExpr = (InvocationExpressionSyntax)context.Node;

        if (invocationExpr.Expression is not MemberAccessExpressionSyntax memberAccessExpr)
            return;

        if (memberAccessExpr.Name.Identifier.Text != "CallActivityAsync") 
            return; 
        
        var argumentList = invocationExpr.ArgumentList.Arguments; 
        if (argumentList.Count == 0) 
            return;

        var firstArgument = argumentList[0].Expression; 
        if (firstArgument is InvocationExpressionSyntax nameofInvocation)
        { 
            var activityName = nameofInvocation.ArgumentList.Arguments.FirstOrDefault()?.Expression.ToString().Trim('"');
            if (activityName != null) 
            { 
                bool isRegistered = CheckIfActivityIsRegistered(activityName, context.SemanticModel); 
                if (!isRegistered) 
                {
                    var diagnostic = Diagnostic.Create(DiagnosticDescriptor, firstArgument.GetLocation(), activityName);
                    context.ReportDiagnostic(diagnostic);
                } 
            } 
        }
    }

    private static bool CheckIfActivityIsRegistered(string activityName, SemanticModel semanticModel)
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
            if (methodName == "RegisterActivity")
            {
                if (memberAccess.Name is GenericNameSyntax typeArgumentList && typeArgumentList.TypeArgumentList.Arguments.Count > 0)
                {
                    if (typeArgumentList.TypeArgumentList.Arguments[0] is IdentifierNameSyntax typeArgument)
                    {                        
                        if (string.Equals(typeArgument.Identifier.Text, activityName))
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
