// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

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
public sealed class MapSubscribeHandlerCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ["DAPR2001"];

    /// <summary>
    /// Gets the FixAllProvider for this code fix provider.
    /// </summary>
    /// <returns>The FixAllProvider.</returns>
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <summary>
    /// Registers code fixes for the specified diagnostics.
    /// </summary>
    /// <param name="context">A <see cref="CodeFixContext"/> containing the context in which the code fix is being applied.</param>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        const string title = "Map Dapr PubSub endpoint handler";
        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                createChangedDocument: c => AddMapSubscribeHandlerAsync(context.Document, context.Diagnostics.First(), c),
                equivalenceKey: title),
            context.Diagnostics);
        return Task.CompletedTask;
    }

    private static async Task<Document> AddMapSubscribeHandlerAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var invocationExpressions = root!.DescendantNodes().OfType<InvocationExpressionSyntax>().ToList();

        var createBuilderInvocation = invocationExpressions
            .FirstOrDefault(invocation => invocation.Expression is MemberAccessExpressionSyntax
            {
                Name.Identifier.Text: "CreateBuilder", Expression: IdentifierNameSyntax
                {
                    Identifier.Text: "WebApplication"
                }
            });

        if (createBuilderInvocation == null)
        {
            return document.WithSyntaxRoot(root);
        }

        var variableDeclarator = createBuilderInvocation
            .AncestorsAndSelf()
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault();

        if (variableDeclarator is null)
        {
            return document.WithSyntaxRoot(root);
        }

        var variableName = variableDeclarator.Identifier.Text;

        var buildInvocation = invocationExpressions
            .FirstOrDefault(invocation => invocation.Expression is MemberAccessExpressionSyntax
                                          {
                                              Name.Identifier.Text: "Build",
                                              Expression: IdentifierNameSyntax identifier
                                          } &&
                                          identifier.Identifier.Text == variableName);

        if (buildInvocation is null)
        {
            return document.WithSyntaxRoot(root);
        }

        var buildVariableDeclarator = buildInvocation
            .AncestorsAndSelf()
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault();

        if (buildVariableDeclarator is null)
        {
            return document.WithSyntaxRoot(root);
        }

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

            var newParentBlock =
                parentBlock.InsertNodesAfter(localDeclaration, [mapSubscribeHandlerInvocation]);
            root = root.ReplaceNode(parentBlock, newParentBlock);
        }
        else
        {
            var buildInvocationGlobalStatement = buildInvocation?
                .AncestorsAndSelf()
                .OfType<GlobalStatementSyntax>()
                .FirstOrDefault();

            var compilationUnitSyntax =
                createBuilderInvocation.Ancestors().OfType<CompilationUnitSyntax>().FirstOrDefault();
            var newCompilationUnitSyntax = compilationUnitSyntax.InsertNodesAfter(buildInvocationGlobalStatement!,
                [SyntaxFactory.GlobalStatement(mapSubscribeHandlerInvocation)]);
            root = root.ReplaceNode(compilationUnitSyntax, newCompilationUnitSyntax);
        }

        return document.WithSyntaxRoot(root);
    }
}
