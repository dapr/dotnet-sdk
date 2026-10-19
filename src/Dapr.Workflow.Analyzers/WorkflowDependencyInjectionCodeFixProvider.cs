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
using Microsoft.CodeAnalysis.FindSymbols;

namespace Dapr.Workflow.Analyzers;

/// <summary>
/// Provides a code fix for DAPR1305 by removing the offending constructor parameter
/// from either a regular constructor or a primary constructor, only when the parameter
/// and constructor have no references and removal cannot change an overload set.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(WorkflowDependencyInjectionCodeFixProvider))]
[Shared]
public sealed class WorkflowDependencyInjectionCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("DAPR1305");

    /// <summary>
    /// Registers the code fix for the diagnostic.
    /// </summary>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        if (!await CanRemoveParameterAsync(context.Document, context.Diagnostics.First(), context.CancellationToken)
                .ConfigureAwait(false))
            return;

        const string title = "Remove injected constructor parameter";
        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                createChangedDocument: c => RemoveParameterAsync(context.Document, context.Diagnostics.First(), c),
                equivalenceKey: title),
            context.Diagnostics);
    }

    private static async Task<bool> CanRemoveParameterAsync(
        Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var parameter = root?.FindToken(diagnostic.Location.SourceSpan.Start).Parent?
            .AncestorsAndSelf().OfType<ParameterSyntax>().FirstOrDefault();
        if (parameter is null || model?.GetDeclaredSymbol(parameter, cancellationToken) is not IParameterSymbol symbol ||
            symbol.ContainingSymbol is not IMethodSymbol constructor)
            return false;

        // Do not rewrite application logic, call sites, or constructor overload sets.
        if (constructor.ContainingType.InstanceConstructors.Count(c => !c.IsImplicitlyDeclared) > 1)
            return false;
        var references = await SymbolFinder.FindReferencesAsync(symbol, document.Project.Solution, cancellationToken)
            .ConfigureAwait(false);
        if (references.Any(r => r.Locations.Any()))
            return false;
        var callers = await SymbolFinder.FindReferencesAsync(constructor, document.Project.Solution, cancellationToken)
            .ConfigureAwait(false);
        return !callers.Any(r => r.Locations.Any());
    }

    private static async Task<Document> RemoveParameterAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        if (!await CanRemoveParameterAsync(document, diagnostic, cancellationToken).ConfigureAwait(false))
            return document;

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
