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

    private static async Task<Document> RegisterWorkflowAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        // Prefer the ScheduleNewWorkflowAsync(...) invocation even if the diagnostic is reported on nameof(...)
        var invocationsAtLocation = root?
            .FindToken(diagnosticSpan.Start)
            .Parent?
            .AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .ToList();

        var oldInvocation = invocationsAtLocation?
                                .FirstOrDefault(invocation =>
                                    invocation.Expression is MemberAccessExpressionSyntax memberAccessExpr &&
                                    memberAccessExpr.Name.Identifier.Text == "ScheduleNewWorkflowAsync")
                            ?? invocationsAtLocation?.FirstOrDefault();

        if (oldInvocation is null || root is null)
            return document;

        // Get the semantic model
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (semanticModel is null)
            return document;

        // Extract the workflow type name from nameof(SomeWorkflow)
        var firstArgExpr = oldInvocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
        if (firstArgExpr is not InvocationExpressionSyntax nameofInvocation ||
            nameofInvocation.Expression is not IdentifierNameSyntax { Identifier.Text: "nameof" } ||
            nameofInvocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is not { } nameofArgExpr)
            return document;
        
        // Get the symbol for the workflow type
        if (semanticModel.GetSymbolInfo(nameofArgExpr, cancellationToken).Symbol is not INamedTypeSymbol workflowTypeSymbol)
            return document;

        // Get the fully qualified name
        var workflowType = workflowTypeSymbol.ToDisplayString(new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces));

        if (string.IsNullOrEmpty(workflowType))
            return document;

        var (targetDocument, addDaprWorkflowInvocation) =
            await FindAddDaprWorkflowInvocationAsync(document.Project, cancellationToken);

        if (addDaprWorkflowInvocation == null)
        {
            (targetDocument, addDaprWorkflowInvocation) =
                await CreateAddDaprWorkflowInvocation(document.Project, cancellationToken);
        }

        if (addDaprWorkflowInvocation == null || targetDocument == null)
            return document;

        var targetRoot = await addDaprWorkflowInvocation.SyntaxTree.GetRootAsync(cancellationToken);
        if (targetRoot == null)
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
        newRoot = Formatter.Format(newRoot, targetDocument.Project.Solution.Workspace);

        return targetDocument.WithSyntaxRoot(newRoot);
    }

    /// <summary>
    /// Gets the FixAllProvider for this code fix provider.
    /// </summary>
    /// <returns>The FixAllProvider instance.</returns>
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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

    private static async Task<(Document?, InvocationExpressionSyntax?)> CreateAddDaprWorkflowInvocation(Project project, CancellationToken cancellationToken)
    {
        // Case 1/2 : var builder = WebApplication.CreateBuilder(...);
        //            var builder = Host.CreateApplicationBuilder(...);
        var createBuilderInvocation = await FindCreateBuilderInvocationAsync(project, cancellationToken);
        if (createBuilderInvocation != null)
        {
            var variableDeclarator = createBuilderInvocation.Ancestors()
                .OfType<VariableDeclaratorSyntax>()
                .FirstOrDefault();

            var builderVariable = variableDeclarator?.Identifier.Text;
            if (string.IsNullOrWhiteSpace(builderVariable))
                return (null, null);

            var targetRoot = await createBuilderInvocation.SyntaxTree.GetRootAsync(cancellationToken);
            var document = project.GetDocument(createBuilderInvocation.SyntaxTree);
            if (targetRoot == null || document == null)
                return (null, null);

            // Force a block-bodied lambda so formatting is stable and matches tests.
            var addDaprWorkflowStatement = SyntaxFactory.ParseStatement(
                $"{builderVariable}.Services.AddDaprWorkflow(options =>\n{{\n}});\n");

            // Insert immediately after the statement containing the builder creation.
            // Handles:
            // - inside a method/body (BlockSyntax)
            // - top-level statements (GlobalStatementSyntax -> CompilationUnitSyntax)
            var containingStatement = createBuilderInvocation.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
            if (containingStatement is null)
                return (null, null);

            if (containingStatement.Parent is BlockSyntax block)
            {
                var newBlock = block.InsertNodesAfter(containingStatement, [addDaprWorkflowStatement]);
                targetRoot = targetRoot.ReplaceNode(block, newBlock);
            }
            else if (containingStatement.Parent is GlobalStatementSyntax globalStatement &&
                     globalStatement.Parent is CompilationUnitSyntax compilationUnitFromGlobal)
            {
                var newCompilationUnit = compilationUnitFromGlobal.InsertNodesAfter(
                    globalStatement,
                    [SyntaxFactory.GlobalStatement(addDaprWorkflowStatement)]);

                targetRoot = targetRoot.ReplaceNode(compilationUnitFromGlobal, newCompilationUnit);
            }
            else if (containingStatement.Parent is CompilationUnitSyntax compilationUnit)
            {
                var newCompilationUnit = compilationUnit.InsertNodesAfter(
                    containingStatement,
                    [SyntaxFactory.GlobalStatement(addDaprWorkflowStatement)]);

                targetRoot = targetRoot.ReplaceNode(compilationUnit, newCompilationUnit);
            }
            else
            {
                return (null, null);
            }

            var addDaprWorkflowInvocation = targetRoot.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                                              memberAccess.Name.Identifier.Text == "AddDaprWorkflow");

            return (document, addDaprWorkflowInvocation);
        }

        // Case 3 : Host.CreateDefaultBuilder(args).ConfigureServices(services => {...});
        var configureServicesInvocation = await FindConfigureServicesInvocationAsync(project, cancellationToken);
        if (configureServicesInvocation != null)
        {
            var document = project.GetDocument(configureServicesInvocation.SyntaxTree);
            var targetRoot = await configureServicesInvocation.SyntaxTree.GetRootAsync(cancellationToken);
            if (targetRoot == null || document == null)
                return (null, null);

            var lambda = configureServicesInvocation.ArgumentList.Arguments
                .Select(a => a.Expression)
                .OfType<LambdaExpressionSyntax>()
                .FirstOrDefault();

            if (lambda is null)
                return (null, null);

            var servicesParamName =
                lambda switch
                {
                    SimpleLambdaExpressionSyntax s => s.Parameter.Identifier.Text,
                    ParenthesizedLambdaExpressionSyntax p => p.ParameterList.Parameters.FirstOrDefault()?.Identifier.Text,
                    _ => null
                };

            if (string.IsNullOrWhiteSpace(servicesParamName))
                return (null, null);

            var addDaprWorkflowStatement = SyntaxFactory.ParseStatement(
                $"{servicesParamName}.AddDaprWorkflow(options =>\n{{\n}});\n");

            SyntaxNode newRoot = targetRoot;

            if (lambda.Body is BlockSyntax bodyBlock)
            {
                var newBodyBlock = bodyBlock.WithStatements(bodyBlock.Statements.Insert(0, addDaprWorkflowStatement));
                newRoot = newRoot.ReplaceNode(bodyBlock, newBodyBlock);
            }
            else if (lambda.Body is ExpressionSyntax exprBody)
            {
                var newBodyBlock = SyntaxFactory.Block(addDaprWorkflowStatement, SyntaxFactory.ExpressionStatement(exprBody));
                newRoot = newRoot.ReplaceNode(exprBody, newBodyBlock);
            }
            else
            {
                return (null, null);
            }

            newRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);

            var updatedDoc = document.WithSyntaxRoot(newRoot);
            var updatedRoot = await updatedDoc.GetSyntaxRootAsync(cancellationToken);
            var addDaprWorkflowInvocation = updatedRoot?.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                                              memberAccess.Name.Identifier.Text == "AddDaprWorkflow");

            return (updatedDoc, addDaprWorkflowInvocation);
        }

        return (null, null);
    }

    private static async Task<InvocationExpressionSyntax?> FindCreateBuilderInvocationAsync(Project project, CancellationToken cancellationToken)
    {
        var compilation = await project.GetCompilationAsync(cancellationToken);

        foreach (var syntaxTree in compilation!.SyntaxTrees)
        {
            var syntaxRoot = await syntaxTree.GetRootAsync(cancellationToken);

            var createBuilderInvocation = syntaxRoot.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(invocation => invocation.Expression is MemberAccessExpressionSyntax
                {
                    Expression: IdentifierNameSyntax { Identifier.Text: "WebApplication" },
                    Name.Identifier.Text: "CreateBuilder"
                } or MemberAccessExpressionSyntax
                {
                    Expression: IdentifierNameSyntax { Identifier.Text: "Host" },
                    Name.Identifier.Text: "CreateApplicationBuilder"
                });

            if (createBuilderInvocation != null)
            {
                return createBuilderInvocation;
            }
        }

        return null;
    }

    private static async Task<InvocationExpressionSyntax?> FindConfigureServicesInvocationAsync(Project project, CancellationToken cancellationToken)
    {
        var compilation = await project.GetCompilationAsync(cancellationToken);

        foreach (var syntaxTree in compilation!.SyntaxTrees)
        {
            var root = await syntaxTree.GetRootAsync(cancellationToken);

            var configureServicesInvocation = root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(invocation =>
                    invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "ConfigureServices" });

            if (configureServicesInvocation != null)
            {
                return configureServicesInvocation;
            }
        }

        return null;
    }
}
