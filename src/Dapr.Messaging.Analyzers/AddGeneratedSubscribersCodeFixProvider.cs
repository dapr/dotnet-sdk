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
/// Provides a code fix for the diagnostic "DAPR1614" by appending a call to
/// <c>.AddGeneratedSubscribers()</c> after <c>.AddDaprSubscriber()</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddGeneratedSubscribersCodeFixProvider))]
[Shared]
public sealed class AddGeneratedSubscribersCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this code fix provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(MissingAddGeneratedSubscribersAnalyzer.DiagnosticId);

    /// <summary>
    /// Gets the FixAllProvider for this code fix provider.
    /// </summary>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <summary>
    /// Registers code fixes for the specified diagnostic.
    /// </summary>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        const string title = "Call .AddGeneratedSubscribers()";
        context.RegisterCodeFix(
            CodeAction.Create(
                title: title,
                createChangedDocument: c => AddGeneratedSubscribersAsync(context.Document, context.Span, c),
                equivalenceKey: title),
            context.Diagnostics);
        return Task.CompletedTask;
    }

    private static async Task<Document> AddGeneratedSubscribersAsync(Document document, Microsoft.CodeAnalysis.Text.TextSpan span, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var node = root.FindNode(span);
        var addDaprSubscriberInvocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>(
            invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                          memberAccess.Name.Identifier.Text == "AddDaprSubscriber");

        if (addDaprSubscriberInvocation is null)
        {
            return document;
        }

        var newInvocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                addDaprSubscriberInvocation,
                SyntaxFactory.IdentifierName("AddGeneratedSubscribers")));

        var newRoot = root.ReplaceNode(addDaprSubscriberInvocation, newInvocation);
        return document.WithSyntaxRoot(newRoot);
    }
}
