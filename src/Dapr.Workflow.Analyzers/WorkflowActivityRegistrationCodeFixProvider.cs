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
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Dapr.Workflow.Analyzers;

/// <summary>
/// Provides code fixes for DAPR1002 diagnostic.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(WorkflowActivityRegistrationCodeFixProvider))]
[Shared]
public sealed class WorkflowActivityRegistrationCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("DAPR1002");

    /// <summary>
    /// Registers the code fix for the diagnostic.
    /// </summary>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        const string title = "Register workflow activity";
        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                createChangedDocument: c => RegisterWorkflowActivityAsync(context.Document, context.Diagnostics.First(), c),
                equivalenceKey: title),
            context.Diagnostics);
        return Task.CompletedTask;
    }

    private async Task<Document> RegisterWorkflowActivityAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var oldInvocation = root?.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().First();

        if (oldInvocation is null)
            return document;

        if (root == null || oldInvocation == null)
            return document;

        // Extract the workflow activity type name
        var workflowActivityType = oldInvocation.ArgumentList.Arguments.FirstOrDefault()?.Expression.ToString();

        if (string.IsNullOrEmpty(workflowActivityType))
            return document;

        // Get the compilation
        var compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);

        if (compilation == null)
            return document;

        InvocationExpressionSyntax? addDaprWorkflowInvocation = null;
        SyntaxNode? targetRoot = null;
        Document? targetDocument = null;

        // Iterate through all syntax trees in the compilation
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var syntaxRoot = await syntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);

            addDaprWorkflowInvocation = syntaxRoot.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                                              memberAccess.Name.Identifier.Text == "AddDaprWorkflow");

            if (addDaprWorkflowInvocation != null)
            {
                targetRoot = syntaxRoot;
                targetDocument = document.Project.GetDocument(syntaxTree);
                break;
            }
        }

        if (addDaprWorkflowInvocation == null || targetRoot == null || targetDocument == null)
            return document;

        // Find the options lambda block
        var optionsLambda = addDaprWorkflowInvocation.ArgumentList.Arguments
            .Select(arg => arg.Expression)
            .OfType<SimpleLambdaExpressionSyntax>()
            .FirstOrDefault();

        // Extract the parameter name from the lambda expression
        var parameterName = optionsLambda?.Parameter.Identifier.Text;

        // Create the new workflow registration statement
        var registerWorkflowStatement = SyntaxFactory.ParseStatement($"{parameterName}.RegisterActivity<{workflowActivityType}>();");

        if (optionsLambda == null || optionsLambda.Body is not BlockSyntax optionsBlock)
            return document;

        // Add the new registration statement to the options block
        var newOptionsBlock = optionsBlock.AddStatements(registerWorkflowStatement);

        // Replace the old options block with the new one
        var newRoot = targetRoot.ReplaceNode(optionsBlock, newOptionsBlock);

        // Format the new root.
        newRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);

        return targetDocument.WithSyntaxRoot(newRoot);
    }

    /// <summary>
    /// Gets the FixAllProvider for this code fix provider.
    /// </summary>
    /// <returns>The FixAllProvider instance.</returns>
    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }
}
