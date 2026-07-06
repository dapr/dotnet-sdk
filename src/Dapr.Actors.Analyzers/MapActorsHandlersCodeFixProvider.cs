using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dapr.Actors.Analyzers;

/// <summary>
/// Provides a code fix for the diagnostic "DAPR0003" by adding a call to MapActorsHandlers.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ActorRegistrationCodeFixProvider))]
[Shared]
public class MapActorsHandlersCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this code fix provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("DAPR0003");

    /// <summary>
    /// Gets the FixAllProvider for this code fix provider.
    /// </summary>
    /// <returns>The FixAllProvider.</returns>
    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    /// <summary>
    /// Registers code fixes for the specified diagnostic.
    /// </summary>
    /// <param name="context">A context for code fix registration.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var title = "Call MapActorsHandlers";
        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                createChangedDocument: c => AddMapActorsHandlersAsync(context.Document, context.Diagnostics.First(), c),
                equivalenceKey: title),
            context.Diagnostics);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Adds a call to MapActorsHandlers to the specified document.
    /// </summary>
    /// <param name="document">The document to modify.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the modified document.</returns>
    private async Task<Document> AddMapActorsHandlersAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var invocationExpressions = root!.DescendantNodes().OfType<InvocationExpressionSyntax>();

        var createBuilderInvocation = invocationExpressions
            .FirstOrDefault(invocation =>
            {
                return invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                        memberAccess.Name.Identifier.Text == "CreateBuilder" &&
                        memberAccess.Expression is IdentifierNameSyntax identifier &&
                        identifier.Identifier.Text == "WebApplication";
            });

        var variableDeclarator = createBuilderInvocation
            .AncestorsAndSelf()
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault();

        var variableName = variableDeclarator.Identifier.Text;

        var buildInvocation = invocationExpressions
            .FirstOrDefault(invocation =>
            {
                return invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                       memberAccess.Name.Identifier.Text == "Build" &&
                       memberAccess.Expression is IdentifierNameSyntax identifier &&
                       identifier.Identifier.Text == variableName;
            });

        var buildVariableDeclarator = buildInvocation
            .AncestorsAndSelf()
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault();

        var buildVariableName = buildVariableDeclarator.Identifier.Text;

        var mapActorsHandlersInvocation = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(buildVariableName),
                    SyntaxFactory.IdentifierName("MapActorsHandlers"))));

        if (buildInvocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is SyntaxNode parentBlock)
        {
            var localDeclaration = buildInvocation
                .AncestorsAndSelf()
                .OfType<LocalDeclarationStatementSyntax>()
                .FirstOrDefault();

            var newParentBlock = parentBlock.InsertNodesAfter(localDeclaration, new[] { mapActorsHandlersInvocation });
            root = root.ReplaceNode(parentBlock, newParentBlock);
        }
        else
        {
            var buildInvocationGlobalStatement = buildInvocation
                .AncestorsAndSelf()
                .OfType<GlobalStatementSyntax>()
                .FirstOrDefault();

            var compilationUnitSyntax = createBuilderInvocation.Ancestors().OfType<CompilationUnitSyntax>().FirstOrDefault();
            var newCompilationUnitSyntax = compilationUnitSyntax.InsertNodesAfter(buildInvocationGlobalStatement,
                new[] { SyntaxFactory.GlobalStatement(mapActorsHandlersInvocation) });
            root = root.ReplaceNode(compilationUnitSyntax, newCompilationUnitSyntax);
        }

        return document.WithSyntaxRoot(root);
    }
}
