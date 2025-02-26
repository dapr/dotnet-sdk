using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dapr.Pubsub.Analyzers;

/// <summary>
/// Provides a code fix for the DAPR2001 diagnostic.
/// </summary>
public class MapSubscribeHandlerCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("DAPR2001");

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
    /// <param name="context">A <see cref="CodeFixContext"/> containing the context in which the code fix is being applied.</param>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var title = "Call MapSubscribeHandler";
        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                createChangedDocument: c => AddMapSubscribeHandlerAsync(context.Document, context.Diagnostics.First(), c),
                equivalenceKey: title),
            context.Diagnostics);
        return Task.CompletedTask;
    }
    
    private async Task<Document> AddMapSubscribeHandlerAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
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

        var mapSubscribeHandlerInvocation = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(buildVariableName),
                    SyntaxFactory.IdentifierName("MapSubscribeHandler"))));

        if (buildInvocation?.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is SyntaxNode parentBlock)
        {
            var localDeclaration = buildInvocation
                .AncestorsAndSelf()
                .OfType<LocalDeclarationStatementSyntax>()
                .FirstOrDefault();

            var newParentBlock = parentBlock.InsertNodesAfter(localDeclaration, new[] { mapSubscribeHandlerInvocation });
            root = root.ReplaceNode(parentBlock, newParentBlock);
        }
        else
        {
            var buildInvocationGlobalStatement = buildInvocation?
                .AncestorsAndSelf()
                .OfType<GlobalStatementSyntax>()
                .FirstOrDefault();

            var compilationUnitSyntax = createBuilderInvocation.Ancestors().OfType<CompilationUnitSyntax>().FirstOrDefault();
            var newCompilationUnitSyntax = compilationUnitSyntax.InsertNodesAfter(buildInvocationGlobalStatement!,
                new[] { SyntaxFactory.GlobalStatement(mapSubscribeHandlerInvocation) });
            root = root.ReplaceNode(compilationUnitSyntax, newCompilationUnitSyntax);
        }

        return document.WithSyntaxRoot(root);
    }
}
