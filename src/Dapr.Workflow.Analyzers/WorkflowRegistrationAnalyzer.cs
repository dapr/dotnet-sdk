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
public class WorkflowRegistrationAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor WorkflowDiagnosticDescriptor = new(
        id: "DAPR1301",
         title: new LocalizableResourceString(nameof(Resources.DAPR1301Title), Resources.ResourceManager, typeof(Resources)),
        messageFormat: new LocalizableResourceString(nameof(Resources.DAPR1301MessageFormat), Resources.ResourceManager, typeof(Resources)),
        category: "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Gets the supported diagnostics for this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [WorkflowDiagnosticDescriptor];

    /// <summary>
    /// Initializes the analyzer.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeWorkflowRegistration, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeWorkflowRegistration(SyntaxNodeAnalysisContext context)
    {
        var invocationExpr = (InvocationExpressionSyntax)context.Node;

        if (invocationExpr.Expression is not MemberAccessExpressionSyntax memberAccessExpr)
            return;

        if (memberAccessExpr.Name.Identifier.Text != "ScheduleNewWorkflowAsync")
            return;

        var argumentList = invocationExpr.ArgumentList.Arguments;
        if (argumentList.Count == 0)
            return;

        var firstArgument = argumentList[0].Expression;
        if (firstArgument is not InvocationExpressionSyntax nameofInvocation ||
            nameofInvocation.Expression is not IdentifierNameSyntax { Identifier.Text : "nameof"} ||
            nameofInvocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is not {} nameofArgExpr)
            return;
        
        if (context.SemanticModel.GetSymbolInfo(nameofArgExpr, context.CancellationToken).Symbol is not INamedTypeSymbol workflowTypeSymbol)
            return;
        
        var isRegistered = CheckIfWorkflowIsRegistered(workflowTypeSymbol, context.SemanticModel, context.CancellationToken);
        if (isRegistered)
        {
            return;
        }
        
        var workflowName = workflowTypeSymbol.Name;
        var diagnostic = Diagnostic.Create(WorkflowDiagnosticDescriptor, firstArgument.GetLocation(), workflowName);
        context.ReportDiagnostic(diagnostic);
    }

    private static bool CheckIfWorkflowIsRegistered(INamedTypeSymbol workflowType, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var methodInvocations = new List<InvocationExpressionSyntax>();
        foreach (var syntaxTree in semanticModel.Compilation.SyntaxTrees)
        {
            var root = syntaxTree.GetRoot(cancellationToken);
            methodInvocations.AddRange(root.DescendantNodes().OfType<InvocationExpressionSyntax>());
        }

        foreach (var invocation in methodInvocations)
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                continue;
            }

            if (memberAccess.Name is not GenericNameSyntax genericName ||
                genericName.Identifier.Text != "RegisterWorkflow" ||
                genericName.TypeArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            var typeArgSyntax = genericName.TypeArgumentList.Arguments[0];
            var typeArgSymbol = semanticModel.GetSymbolInfo(typeArgSyntax, cancellationToken).Symbol as INamedTypeSymbol;
            if (typeArgSymbol is null)
            {
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(typeArgSymbol, workflowType))
            {
                return true;
            }
        }

        return false;
    }
}
