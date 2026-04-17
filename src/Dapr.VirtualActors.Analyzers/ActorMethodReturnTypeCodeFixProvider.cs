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
/// Provides a code fix for DAPRVACT004 by changing a non-Task return type on an actor
/// interface method to <c>Task</c> or <c>Task&lt;T&gt;</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ActorMethodReturnTypeCodeFixProvider))]
[Shared]
public sealed class ActorMethodReturnTypeCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(AnalyzerDiagnostics.ActorMethodMustReturnTask.Id);

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

        var methodDecl = root.FindToken(diagnosticSpan.Start)
            .Parent?
            .AncestorsAndSelf()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();

        if (methodDecl is null)
            return;

        // Determine whether to suggest Task or Task<T> based on the current return type
        var currentReturnType = methodDecl.ReturnType.ToString();
        var isVoid = currentReturnType is "void" or "System.Void";

        if (isVoid)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Change return type to Task",
                    createChangedDocument: ct =>
                        ChangeReturnTypeAsync(context.Document, methodDecl, "Task", ct),
                    equivalenceKey: nameof(ActorMethodReturnTypeCodeFixProvider) + "Task"),
                diagnostic);
        }
        else
        {
            // Non-void, non-Task type — wrap it in Task<T>
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Change return type to Task<{currentReturnType}>",
                    createChangedDocument: ct =>
                        ChangeReturnTypeAsync(context.Document, methodDecl,
                            $"System.Threading.Tasks.Task<{currentReturnType}>", ct),
                    equivalenceKey: nameof(ActorMethodReturnTypeCodeFixProvider) + "TaskOfT"),
                diagnostic);

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Change return type to Task (drop return value)",
                    createChangedDocument: ct =>
                        ChangeReturnTypeAsync(context.Document, methodDecl, "Task", ct),
                    equivalenceKey: nameof(ActorMethodReturnTypeCodeFixProvider) + "TaskDrop"),
                diagnostic);
        }
    }

    private static async Task<Document> ChangeReturnTypeAsync(
        Document document,
        MethodDeclarationSyntax methodDecl,
        string newTypeName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        var newReturnType = ParseTypeName(newTypeName)
            .WithTriviaFrom(methodDecl.ReturnType);

        var updatedMethod = methodDecl.WithReturnType(newReturnType);
        var newRoot = root.ReplaceNode(methodDecl, updatedMethod);
        return document.WithSyntaxRoot(newRoot);
    }
}
