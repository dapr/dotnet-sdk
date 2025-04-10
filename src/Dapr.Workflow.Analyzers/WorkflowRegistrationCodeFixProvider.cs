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
using Microsoft.CodeAnalysis.Formatting;

namespace Dapr.Workflow.Analyzers;

/// <summary>
/// Provides code fixes for DAPR1001 diagnostic.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(WorkflowRegistrationCodeFixProvider))]
public sealed class WorkflowRegistrationCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ["DAPR1001"];

    /// <summary>
    /// Registers the code fix for the diagnostic.
    /// </summary>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        const string title = "Register Dapr workflow";
        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                createChangedDocument: c => RegisterWorkflowAsync(context.Document, context.Diagnostics.First(), c),
                equivalenceKey: title),
            context.Diagnostics);
        return Task.CompletedTask;
    }

    private async Task<Document> RegisterWorkflowAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var oldInvocation = root?.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().First();

        if (oldInvocation is null || root is null)
        {
            return document;
        }

        // Get the semantic model
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

        // Extract the workflow type name
        var workflowTypeSyntax = oldInvocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;

        if (workflowTypeSyntax == null)
        {
            return document;
        }

        // Get the symbol for the workflow type
        if (semanticModel.GetSymbolInfo(workflowTypeSyntax, cancellationToken).Symbol is not INamedTypeSymbol
            workflowTypeSymbol)
        {
            return document;
        }

        // Get the fully qualified name
        var workflowType = workflowTypeSymbol.ToDisplayString(new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces));

        if (string.IsNullOrEmpty(workflowType))
        {
            return document;
        }

        // Get the compilation
        var compilation = await document.Project.GetCompilationAsync(cancellationToken);

        if (compilation == null)
        {
            return document;
        }

        var (targetDocument, addDaprWorkflowInvocation) = await FindAddDaprWorkflowInvocationAsync(document.Project, cancellationToken);

        if (addDaprWorkflowInvocation == null)
        {
            (targetDocument, addDaprWorkflowInvocation) = await CreateAddDaprWorkflowInvocation(document.Project, cancellationToken);
        }

        if (addDaprWorkflowInvocation == null)
        {
            return document;
        }

        var targetRoot = await addDaprWorkflowInvocation.SyntaxTree.GetRootAsync(cancellationToken);

        if (targetRoot == null || targetDocument == null)
            return document;

        // Find the options lambda block
        var optionsLambda = addDaprWorkflowInvocation.ArgumentList.Arguments
            .Select(arg => arg.Expression)
            .OfType<SimpleLambdaExpressionSyntax>()
            .FirstOrDefault();

        if (optionsLambda is not { Body: BlockSyntax optionsBlock })
            return document;

        // Extract the parameter name from the lambda expression
        var parameterName = optionsLambda.Parameter.Identifier.Text;

        // Create the new workflow registration statement
        var registerWorkflowStatement = SyntaxFactory.ParseStatement($"{parameterName}.RegisterWorkflow<{workflowType}>();");

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

    private static async Task<(Document?, InvocationExpressionSyntax?)> FindAddDaprWorkflowInvocationAsync(Project project, CancellationToken cancellationToken)
    {
        var compilation = await project.GetCompilationAsync(cancellationToken);

        foreach (var syntaxTree in compilation!.SyntaxTrees)
        {
            var syntaxRoot = await syntaxTree.GetRootAsync(cancellationToken);
            
            var addDaprWorkflowInvocation = syntaxRoot.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                                              memberAccess.Name.Identifier.Text == "AddDaprWorkflow");

            if (addDaprWorkflowInvocation == null)
            {
                continue;
            }

            var document = project.GetDocument(addDaprWorkflowInvocation.SyntaxTree);
            return (document, addDaprWorkflowInvocation);
        }

        return (null, null);
    }

    private async Task<(Document?, InvocationExpressionSyntax?)> CreateAddDaprWorkflowInvocation(Project project, CancellationToken cancellationToken)
    {
        var createBuilderInvocation = await FindCreateBuilderInvocationAsync(project, cancellationToken);

        var variableDeclarator = createBuilderInvocation?.Ancestors()
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault();

        var builderVariable = variableDeclarator?.Identifier.Text;

        if (createBuilderInvocation == null)
        {
            return (null, null);
        }

        var targetRoot = await createBuilderInvocation.SyntaxTree.GetRootAsync(cancellationToken);
        var document = project.GetDocument(createBuilderInvocation.SyntaxTree);

        if (createBuilderInvocation.Expression is not MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax })
        {
            return (null, null);
        }

        var addDaprWorkflowStatement = SyntaxFactory.ParseStatement($"{builderVariable}.Services.AddDaprWorkflow(options => {{ }});");

        if (createBuilderInvocation.Ancestors().OfType<BlockSyntax>().FirstOrDefault() is SyntaxNode parentBlock)
        {
            var firstChild = parentBlock.ChildNodes().FirstOrDefault(node => node is not UsingDirectiveSyntax);
            var newParentBlock = parentBlock.InsertNodesAfter(firstChild, new[] { addDaprWorkflowStatement });
            targetRoot = targetRoot.ReplaceNode(parentBlock, newParentBlock);
        }
        else
        {
            var compilationUnitSyntax = createBuilderInvocation.Ancestors().OfType<CompilationUnitSyntax>().FirstOrDefault();
            if (compilationUnitSyntax != null)
            {
                var firstChild = compilationUnitSyntax.ChildNodes().FirstOrDefault(node => node is not UsingDirectiveSyntax);
                var globalStatement = SyntaxFactory.GlobalStatement(addDaprWorkflowStatement);
                var newCompilationUnitSyntax = compilationUnitSyntax.InsertNodesAfter(firstChild, [globalStatement]);
                targetRoot = targetRoot.ReplaceNode(compilationUnitSyntax, newCompilationUnitSyntax);
            }
        }

        var addDaprWorkflowInvocation = targetRoot?.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                                          memberAccess.Name.Identifier.Text == "AddDaprWorkflow");

        return (document, addDaprWorkflowInvocation);

    }

    private static async Task<InvocationExpressionSyntax?> FindCreateBuilderInvocationAsync(Project project, CancellationToken cancellationToken)
    {
        var compilation = await project.GetCompilationAsync(cancellationToken);

        foreach (var syntaxTree in compilation!.SyntaxTrees)
        {
            var syntaxRoot = await syntaxTree.GetRootAsync(cancellationToken);

            // Find the invocation expression for WebApplication.CreateBuilder()
            var createBuilderInvocation = syntaxRoot.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(invocation => invocation.Expression is MemberAccessExpressionSyntax
                {
                    Expression: IdentifierNameSyntax
                    {
                        Identifier.Text: "WebApplication"
                    },
                    Name.Identifier.Text: "CreateBuilder"
                });

            if (createBuilderInvocation != null)
            {
                return createBuilderInvocation;
            }
        }

        return null;
    }
}
