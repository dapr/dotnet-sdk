// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dapr.Messaging.Analyzers;

/// <summary>
/// Provides a code fix for the diagnostic "DAPR1613" by adding a call to <c>app.MapDaprAppCallback()</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MapDaprAppCallbackCodeFixProvider))]
[Shared]
public sealed class MapDaprAppCallbackCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this code fix provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(MissingMapDaprAppCallbackAnalyzer.DiagnosticId);

    /// <summary>
    /// Gets the FixAllProvider for this code fix provider.
    /// </summary>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <summary>
    /// Registers code fixes for the specified diagnostic.
    /// </summary>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        const string title = "Call app.MapDaprAppCallback()";
        context.RegisterCodeFix(
            CodeAction.Create(
                title: title,
                createChangedDocument: c => AddMapDaprAppCallbackAsync(context.Document, c),
                equivalenceKey: title),
            context.Diagnostics);
        return Task.CompletedTask;
    }

    private static async Task<Document> AddMapDaprAppCallbackAsync(Document document, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var invocationExpressions = root.DescendantNodes().OfType<InvocationExpressionSyntax>().ToList();

        // 1. Find the WebApplication.CreateBuilder (or Host.CreateDefaultBuilder) invocation.
        var createBuilderInvocation = invocationExpressions
            .FirstOrDefault(invocation =>
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.Text == "CreateBuilder" &&
                memberAccess.Expression is IdentifierNameSyntax identifier &&
                identifier.Identifier.Text == "WebApplication");

        var variableName = "builder";
        if (createBuilderInvocation != null)
        {
            var variableDeclarator = createBuilderInvocation
                .AncestorsAndSelf()
                .OfType<VariableDeclaratorSyntax>()
                .FirstOrDefault();

            if (variableDeclarator != null)
            {
                variableName = variableDeclarator.Identifier.Text;
            }
        }

        // 2. Find the builder.Build() invocation.
        var buildInvocation = invocationExpressions
            .FirstOrDefault(invocation =>
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.Text == "Build" &&
                memberAccess.Expression is IdentifierNameSyntax identifier &&
                identifier.Identifier.Text == variableName)
            ?? invocationExpressions
            .FirstOrDefault(invocation =>
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.Text == "Build");

        if (buildInvocation == null)
        {
            return document;
        }

        var buildVariableDeclarator = buildInvocation
            .AncestorsAndSelf()
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault();

        var buildVariableName = buildVariableDeclarator?.Identifier.Text ?? "app";

        // 3. Create the app.MapDaprAppCallback(); statement.
        var mapInvocation = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(buildVariableName),
                    SyntaxFactory.IdentifierName("MapDaprAppCallback"))))
            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

        var localDeclaration = buildInvocation
            .AncestorsAndSelf()
            .OfType<LocalDeclarationStatementSyntax>()
            .FirstOrDefault();

        if (localDeclaration != null)
        {
            if (buildInvocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is SyntaxNode parentBlock)
            {
                var newParentBlock = parentBlock.InsertNodesAfter(localDeclaration, new[] { mapInvocation });
                root = root.ReplaceNode(parentBlock, newParentBlock);
            }
            else if (localDeclaration.Parent is GlobalStatementSyntax globalStatement &&
                     globalStatement.Parent is CompilationUnitSyntax compilationUnit)
            {
                var newCompilationUnit = compilationUnit.InsertNodesAfter(
                    globalStatement,
                    new[] { SyntaxFactory.GlobalStatement(mapInvocation) });
                root = root.ReplaceNode(compilationUnit, newCompilationUnit);
            }
        }

        return document.WithSyntaxRoot(root);
    }
}
