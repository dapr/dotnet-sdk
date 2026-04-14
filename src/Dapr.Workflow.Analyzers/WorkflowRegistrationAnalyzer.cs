using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dapr.Workflow.Analyzers;

/// <summary>
/// Analyzes whether or not workflow activities are registered.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class WorkflowRegistrationAnalyzer : DiagnosticAnalyzer
{
    private const string WorkflowVersioningExtensionsMetadataName =
        "Dapr.Workflow.Versioning.WorkflowVersioningServiceCollectionExtensions";

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
        context.RegisterCompilationStartAction(compilationContext =>
        {
            var versioningExtensionsType = compilationContext.Compilation
                .GetTypeByMetadataName(WorkflowVersioningExtensionsMetadataName);

            if (versioningExtensionsType is null)
            {
                // Versioning package is not referenced; use the direct reporting path.
                compilationContext.RegisterSyntaxNodeAction(AnalyzeWorkflowRegistration, SyntaxKind.InvocationExpression);
                return;
            }

            // Versioning package is referenced. Use the deferred-diagnostics pattern so that
            // AddDaprWorkflowVersioning can be verified semantically (avoiding RS1030) while
            // still checking explicit RegisterWorkflow<T> registrations per workflow call.
            // Node actions can execute concurrently, so both collections are thread-safe and
            // results are only acted on in the compilation-end action (which runs after all
            // node actions have finished).
            int versioningCalled = 0;
            var pendingDiagnostics = new ConcurrentBag<Diagnostic>();

            compilationContext.RegisterSyntaxNodeAction(nodeContext =>
            {
                var invocation = (InvocationExpressionSyntax)nodeContext.Node;

                // Semantic check: verify the call resolves to the Dapr versioning extension method.
                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Name.Identifier.Text == "AddDaprWorkflowVersioning" &&
                    nodeContext.SemanticModel.GetSymbolInfo(invocation, nodeContext.CancellationToken).Symbol is IMethodSymbol method &&
                    SymbolEqualityComparer.Default.Equals(method.ContainingType, versioningExtensionsType))
                {
                    Interlocked.Exchange(ref versioningCalled, 1);
                }
            }, SyntaxKind.InvocationExpression);

            compilationContext.RegisterSyntaxNodeAction(nodeContext =>
            {
                // Collect potential DAPR1301 diagnostics; explicit RegisterWorkflow<T>
                // registrations are still respected here.
                var diagnostic = TryBuildWorkflowDiagnostic(nodeContext);
                if (diagnostic is not null)
                    pendingDiagnostics.Add(diagnostic);
            }, SyntaxKind.InvocationExpression);

            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                // If AddDaprWorkflowVersioning was confirmed, all workflows are auto-registered
                // by the source generator — suppress any pending DAPR1301 diagnostics.
                if (Volatile.Read(ref versioningCalled) == 1)
                    return;

                foreach (var d in pendingDiagnostics)
                    endContext.ReportDiagnostic(d);
            });
        });
    }

    private static void AnalyzeWorkflowRegistration(SyntaxNodeAnalysisContext context)
    {
        var diagnostic = TryBuildWorkflowDiagnostic(context);
        if (diagnostic is not null)
            context.ReportDiagnostic(diagnostic);
    }

    private static Diagnostic? TryBuildWorkflowDiagnostic(SyntaxNodeAnalysisContext context)
    {
        var invocationExpr = (InvocationExpressionSyntax)context.Node;

        if (invocationExpr.Expression is not MemberAccessExpressionSyntax memberAccessExpr)
            return null;

        if (memberAccessExpr.Name.Identifier.Text != "ScheduleNewWorkflowAsync")
            return null;

        var argumentList = invocationExpr.ArgumentList.Arguments;
        if (argumentList.Count == 0)
            return null;

        var firstArgument = argumentList[0].Expression;
        if (firstArgument is not InvocationExpressionSyntax nameofInvocation ||
            nameofInvocation.Expression is not IdentifierNameSyntax { Identifier.Text: "nameof" } ||
            nameofInvocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is not { } nameofArgExpr)
            return null;

        if (context.SemanticModel.GetSymbolInfo(nameofArgExpr, context.CancellationToken).Symbol is not INamedTypeSymbol workflowTypeSymbol)
            return null;

        if (CheckIfWorkflowIsRegistered(workflowTypeSymbol, context.SemanticModel, context.CancellationToken))
            return null;

        return Diagnostic.Create(WorkflowDiagnosticDescriptor, firstArgument.GetLocation(), workflowTypeSymbol.Name);
    }

    private static bool CheckIfWorkflowIsRegistered(INamedTypeSymbol workflowType, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        foreach (var syntaxTree in semanticModel.Compilation.SyntaxTrees)
        {
            var root = syntaxTree.GetRoot(cancellationToken);
            var isSameTree = syntaxTree == semanticModel.SyntaxTree;

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
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

                if (isSameTree)
                {
                    // Use full semantic comparison for nodes in the same tree.
                    var typeArgSymbol = semanticModel.GetSymbolInfo(typeArgSyntax, cancellationToken).Symbol as INamedTypeSymbol;
                    if (typeArgSymbol is not null && SymbolEqualityComparer.Default.Equals(typeArgSymbol, workflowType))
                        return true;
                }
                else
                {
                    // For nodes in other trees we cannot use this semantic model (RS1030 prevents
                    // calling Compilation.GetSemanticModel). Fall back to a syntactic name
                    // comparison, which is sufficient for the common case of non-generic workflow
                    // types with distinct names.
                    if (typeArgSyntax is IdentifierNameSyntax identifierName &&
                        identifierName.Identifier.Text == workflowType.Name)
                        return true;
                }
            }
        }

        return false;
    }
}
