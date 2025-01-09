using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Dapr.Workflow.Analyzers;

/// <summary>
/// Provides code fixes for DAPR1001 diagnostic.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(WorkflowRegistrationCodeFixProvider))]
[Shared]
public class WorkflowRegistrationCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("DAPR1001");

    /// <summary>
    /// Registers the code fix for the diagnostic.
    /// </summary>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var title = "Register workflow";
        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                createChangedDocument: c => RegisterWorkflowAsync(context.Document, context.Diagnostics.First(), c),
                equivalenceKey: title),
            context.Diagnostics);
        return Task.CompletedTask;
    }

    private async Task<Document> RegisterWorkflowAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var oldInvocation = root?.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().First();

        if (oldInvocation is null)
            return document;

        if (root == null || oldInvocation == null)
            return document;

        // Extract the workflow type name
        var workflowType = oldInvocation.ArgumentList.Arguments.FirstOrDefault()?.Expression.ToString();

        if (string.IsNullOrEmpty(workflowType))
            return document;

        // Get the compilation
        var compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);

        if (compilation == null)
            return document;

        InvocationExpressionSyntax? addDaprWorkflowInvocation = null;
        SyntaxNode? targetRoot = null;
        Document? targetDocument = null;

        // Iterate through all syntax trees in the compilation
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var syntaxRoot = await syntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);

            addDaprWorkflowInvocation = syntaxRoot.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                                              memberAccess.Name.Identifier.Text == "AddDaprWorkflow");

            if (addDaprWorkflowInvocation != null)
            {
                targetRoot = syntaxRoot;
                targetDocument = document.Project.GetDocument(syntaxTree);
                break;
            }
        }

        if (addDaprWorkflowInvocation == null || targetRoot == null || targetDocument == null)
            return document;

        // Find the options lambda block
        var optionsLambda = addDaprWorkflowInvocation.ArgumentList.Arguments
            .Select(arg => arg.Expression)
            .OfType<SimpleLambdaExpressionSyntax>()
            .FirstOrDefault();

        if (optionsLambda == null || optionsLambda.Body is not BlockSyntax optionsBlock)
            return document;

        // Extract the parameter name from the lambda expression
        var parameterName = optionsLambda.Parameter.Identifier.Text;

        // Create the new workflow registration statement
        var registerWorkflowStatement = SyntaxFactory.ParseStatement($"{parameterName}.RegisterWorkflow<{workflowType}>();");

        // Add the new registration statement to the options block
        var newOptionsBlock = optionsBlock.AddStatements(registerWorkflowStatement);

        // Replace the old options block with the new one
        var newRoot = targetRoot.ReplaceNode(optionsBlock, newOptionsBlock);

        // Format the new root.
        newRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);

        return targetDocument.WithSyntaxRoot(newRoot);
    }

    /// <summary>
    /// Gets the FixAllProvider for this code fix provider.
    /// </summary>
    /// <returns>The FixAllProvider instance.</returns>
    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }
}
