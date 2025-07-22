using System.Composition;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace Dapr.Actors.Analyzers;

/// <summary>
/// Provides code fix to enable JSON serialization for actors.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ActorRegistrationCodeFixProvider))]
[Shared]
public class ActorJsonSerializationCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("DAPR0002");

    /// <summary>
    /// Gets the FixAllProvider for this code fix provider.
    /// </summary>
    /// <returns>The FixAllProvider.</returns>
    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    /// <summary>
    /// Registers code fixes for the specified diagnostics.
    /// </summary>
    /// <param name="context">The context to register the code fixes.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var title = "Use JSON serialization";
        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                createChangedDocument: c => UseJsonSerializationAsync(context.Document, context.Diagnostics.First(), c),
                equivalenceKey: title),
            context.Diagnostics);
        return Task.CompletedTask;
    }

    private async Task<Document> UseJsonSerializationAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        (_, var addActorsInvocation) = await FindAddActorsInvocationAsync(document.Project, cancellationToken);

        if (addActorsInvocation == null)
        {
            return document;
        }

        var optionsLambda = addActorsInvocation?.ArgumentList.Arguments
            .Select(arg => arg.Expression)
            .OfType<SimpleLambdaExpressionSyntax>()
            .FirstOrDefault();

        if (optionsLambda == null || optionsLambda.Body is not BlockSyntax optionsBlock)
            return document;

        // Extract the parameter name from the lambda expression
        var parameterName = optionsLambda.Parameter.Identifier.Text;

        // Check if the lambda body already contains the assignment
        var assignmentExists = optionsBlock.Statements
            .OfType<ExpressionStatementSyntax>()
            .Any(statement => statement.Expression is AssignmentExpressionSyntax assignment &&
                              assignment.Left is MemberAccessExpressionSyntax memberAccess &&
                              memberAccess.Name is IdentifierNameSyntax identifier &&
                              identifier.Identifier.Text == parameterName &&
                              memberAccess.Name.Identifier.Text == "UseJsonSerialization");

        if (!assignmentExists)
        {
            var assignmentStatement = SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(parameterName),
                        SyntaxFactory.IdentifierName("UseJsonSerialization")),
                    SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)));

            var newOptionsBlock = optionsBlock.AddStatements(assignmentStatement);
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root?.ReplaceNode(optionsBlock, newOptionsBlock);
            return document.WithSyntaxRoot(newRoot!);
        }

        return document;
    }

    private async Task<(Document?, InvocationExpressionSyntax?)> FindAddActorsInvocationAsync(Project project, CancellationToken cancellationToken)
    {
        var compilation = await project.GetCompilationAsync(cancellationToken);

        foreach (var syntaxTree in compilation!.SyntaxTrees)
        {
            var syntaxRoot = await syntaxTree.GetRootAsync(cancellationToken);

            var addActorsInvocation = syntaxRoot.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                                              memberAccess.Name.Identifier.Text == "AddActors");

            if (addActorsInvocation != null)
            {
                var document = project.GetDocument(addActorsInvocation.SyntaxTree);
                return (document, addActorsInvocation);
            }
        }

        return (null, null);
    }
}
