// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dapr.Actors.Analyzers;

/// <summary>
/// 
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MappedActorHandlersCodeFixProvider))]
public sealed class MappedActorHandlersCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// A list of diagnostic IDs that this provider can provide fixes for.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("DAPR4004");
    
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
        const string title = "Register Dapr actor mappings";
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
    private static async Task<Document> AddMapActorsHandlersAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var invocationExpressions = root!.DescendantNodes().OfType<InvocationExpressionSyntax>();

        var createBuilderInvocation = invocationExpressions
            .FirstOrDefault(invocation => invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "CreateBuilder", Expression: IdentifierNameSyntax
            {
                Identifier.Text: "WebApplication"
            }
            });

        var variableDeclarator = createBuilderInvocation
            .AncestorsAndSelf()
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault();

        var variableName = variableDeclarator.Identifier.Text;

        var buildInvocation = invocationExpressions
            .FirstOrDefault(invocation => invocation.Expression is MemberAccessExpressionSyntax
                                          {
                                              Name.Identifier.Text: "Build",
                                              Expression: IdentifierNameSyntax identifier
                                          } &&
                                          identifier.Identifier.Text == variableName);

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

            var newParentBlock = parentBlock.InsertNodesAfter(localDeclaration, [mapActorsHandlersInvocation]);
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
                [SyntaxFactory.GlobalStatement(mapActorsHandlersInvocation)]);
            root = root.ReplaceNode(compilationUnitSyntax, newCompilationUnitSyntax);
        }

        return document.WithSyntaxRoot(root);
    }
}
