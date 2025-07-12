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
/// Provides the code fix to enable JSON serialization for Dapr actors.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferActorJsonSerializationCodeFixProvider))]
public sealed class PreferActorJsonSerializationCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// A list of diagnostic IDs that this provider can provide fixes for.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("DAPR4003");
    
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
        const string title = "Use JSON serialization";
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
        var (_, addActorsInvocation) = await FindAddActorsInvocationAsync(document.Project, cancellationToken);

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

        if (assignmentExists)
        {
            return document;
        }

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
