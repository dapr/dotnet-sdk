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

namespace Dapr.Workflow.Analyzers;

/// <summary>
/// Provides a code fix for DAPR1305 by removing the offending constructor parameter
/// from either a regular constructor or a primary constructor.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(WorkflowDependencyInjectionCodeFixProvider))]
[Shared]
public sealed class WorkflowDependencyInjectionCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ["DAPR1305"];

    /// <summary>
    /// Registers the code fix for the diagnostic.
    /// </summary>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        const string title = "Remove injected constructor parameter";
        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                createChangedDocument: c => RemoveParameterAsync(context.Document, context.Diagnostics.First(), c),
                equivalenceKey: title),
            context.Diagnostics);
        return Task.CompletedTask;
    }

    private static async Task<Document> RemoveParameterAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var parameter = root
            .FindToken(diagnosticSpan.Start)
            .Parent?
            .AncestorsAndSelf()
            .OfType<ParameterSyntax>()
            .FirstOrDefault();

        if (parameter is null)
        {
            return document;
        }

        var paramList = (ParameterListSyntax)parameter.Parent!;
        var removedIndex = paramList.Parameters.IndexOf(parameter);
        var newParameters = paramList.Parameters.Remove(parameter);

        // When the first parameter is removed, the next parameter inherits the leading
        // whitespace trivia that was on the removed separator, producing "( Type b)"
        // instead of "(Type b)". Strip it so the result is clean.
        if (removedIndex == 0 && newParameters.Count > 0)
        {
            var firstParam = newParameters[0];
            var newFirstParam = firstParam.WithLeadingTrivia(SyntaxFactory.TriviaList());
            newParameters = newParameters.Replace(firstParam, newFirstParam);
        }

        var newParamList = paramList.WithParameters(newParameters);
        var newRoot = root.ReplaceNode(paramList, newParamList);

        return document.WithSyntaxRoot(newRoot);
    }

    /// <summary>
    /// Gets the FixAllProvider for this code fix provider.
    /// </summary>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;
}
