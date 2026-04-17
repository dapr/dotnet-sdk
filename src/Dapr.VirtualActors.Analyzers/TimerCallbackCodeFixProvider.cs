// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Dapr.VirtualActors.Analyzers;

/// <summary>
/// Provides a code fix for DAPRVACT002 by generating a stub timer callback method
/// on the actor class.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TimerCallbackCodeFixProvider))]
[Shared]
public sealed class TimerCallbackCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(AnalyzerDiagnostics.TimerCallbackNotFound.Id);

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        // Extract the callback name from the diagnostic message
        // Format: "Timer callback method '{0}' does not exist on actor type '{1}'"
        var callbackName = diagnostic.Properties.TryGetValue("CallbackName", out var name)
            ? name
            : ExtractCallbackNameFromNode(root, diagnosticSpan);

        if (string.IsNullOrWhiteSpace(callbackName))
            return;

        var classDecl = root.FindToken(diagnosticSpan.Start)
            .Parent?
            .AncestorsAndSelf()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();

        if (classDecl is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Generate timer callback '{callbackName}'",
                createChangedDocument: ct =>
                    GenerateCallbackMethodAsync(context.Document, classDecl, callbackName!, ct),
                equivalenceKey: nameof(TimerCallbackCodeFixProvider) + callbackName),
            diagnostic);
    }

    private static string? ExtractCallbackNameFromNode(SyntaxNode root, Microsoft.CodeAnalysis.Text.TextSpan span)
    {
        var node = root.FindNode(span);
        if (node is LiteralExpressionSyntax literal)
            return literal.Token.ValueText;

        // nameof(MethodName) case
        if (node is InvocationExpressionSyntax invocation &&
            invocation.Expression is IdentifierNameSyntax id &&
            id.Identifier.Text == "nameof" &&
            invocation.ArgumentList.Arguments.Count == 1 &&
            invocation.ArgumentList.Arguments[0].Expression is IdentifierNameSyntax callbackId)
        {
            return callbackId.Identifier.Text;
        }

        return null;
    }

    private static async Task<Document> GenerateCallbackMethodAsync(
        Document document,
        ClassDeclarationSyntax classDecl,
        string callbackName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        // Generate: public Task <CallbackName>(byte[] data) => Task.CompletedTask;
        var methodStub = MethodDeclaration(
                ParseTypeName("System.Threading.Tasks.Task"),
                Identifier(callbackName))
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(
                Parameter(Identifier("data")).WithType(ParseTypeName("byte[]")))
            .WithBody(Block(
                ReturnStatement(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("Task"),
                        IdentifierName("CompletedTask")))))
            .WithLeadingTrivia(TriviaList(
                Whitespace("    "),
                Comment("// TODO: Implement timer callback"),
                CarriageReturnLineFeed,
                Whitespace("    ")));

        var updatedClass = classDecl.AddMembers(methodStub);
        var newRoot = root.ReplaceNode(classDecl, updatedClass);
        return document.WithSyntaxRoot(newRoot);
    }
}
