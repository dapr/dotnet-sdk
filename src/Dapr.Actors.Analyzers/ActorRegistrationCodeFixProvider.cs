using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Dapr.Actors.Analyzers;

/// <summary>
/// Provides code fixes for actor registration issues.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ActorRegistrationCodeFixProvider))]
[Shared]
public class ActorRegistrationCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic IDs that this provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("DAPR0001");

    /// <summary>
    /// Registers code fixes for the specified diagnostics.
    /// </summary>
    /// <param name="context">The context to register the code fixes.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var title = "Register actor";
        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                createChangedDocument: c => RegisterActorAsync(context.Document, context.Diagnostics.First(), c),
                equivalenceKey: title),
            context.Diagnostics);
        return Task.CompletedTask;
    }
    
    private async Task<Document> RegisterActorAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var classDeclaration = root?.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().First();

        if (root == null || classDeclaration == null)
            return document;

        // Get the semantic model
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

        if (semanticModel == null)
            return document;

        // Get the symbol for the class declaration

        if (semanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken) is not INamedTypeSymbol classSymbol)
            return document;

        // Get the fully qualified name
        var actorType = classSymbol.ToDisplayString(new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces));
        
        if (string.IsNullOrEmpty(actorType))
            return document;

        // Get the compilation
        var compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);

        if (compilation == null)
            return document;

        (var targetDocument, var addActorsInvocation) = await FindAddActorsInvocationAsync(document.Project, cancellationToken);

        if (addActorsInvocation == null)
        {
            (targetDocument, addActorsInvocation) = await CreateAddActorsInvocation(document.Project, cancellationToken);
        }

        if (addActorsInvocation == null)
            return document;

        var targetRoot = await addActorsInvocation.SyntaxTree.GetRootAsync(cancellationToken);

        if (targetRoot == null || targetDocument == null)
            return document;

        // Find the options lambda block
        var optionsLambda = addActorsInvocation?.ArgumentList.Arguments
            .Select(arg => arg.Expression)
            .OfType<SimpleLambdaExpressionSyntax>()
            .FirstOrDefault();

        if (optionsLambda == null || optionsLambda.Body is not BlockSyntax optionsBlock)
            return document;

        // Extract the parameter name from the lambda expression
        var parameterName = optionsLambda.Parameter.Identifier.Text;

        // Create the new workflow registration statement
        var registerWorkflowStatement = SyntaxFactory.ParseStatement($"{parameterName}.Actors.RegisterActor<{actorType}>();");

        // Add the new registration statement to the options block
        var newOptionsBlock = optionsBlock.AddStatements(registerWorkflowStatement);

        // Replace the old options block with the new one
        var newRoot = targetRoot?.ReplaceNode(optionsBlock, newOptionsBlock);

        // Format the new root.
        newRoot = Formatter.Format(newRoot!, document.Project.Solution.Workspace);

        return targetDocument.WithSyntaxRoot(newRoot);
    }

    /// <summary>
    /// Gets the FixAllProvider for this code fix provider.
    /// </summary>
    /// <returns>The FixAllProvider.</returns>
    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
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

    private async Task<InvocationExpressionSyntax?> FindCreateBuilderInvocationAsync(Project project, CancellationToken cancellationToken)
    {
        var compilation = await project.GetCompilationAsync(cancellationToken);

        foreach (var syntaxTree in compilation!.SyntaxTrees)
        {
            var syntaxRoot = await syntaxTree.GetRootAsync(cancellationToken);

            // Find the invocation expression for WebApplication.CreateBuilder()
            var createBuilderInvocation = syntaxRoot.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                                              memberAccess.Expression is IdentifierNameSyntax identifier &&
                                              identifier.Identifier.Text == "WebApplication" &&
                                              memberAccess.Name.Identifier.Text == "CreateBuilder");

            if (createBuilderInvocation != null)
            {
                return createBuilderInvocation;
            }
        }

        return null;
    }

    private async Task<(Document?, InvocationExpressionSyntax?)> CreateAddActorsInvocation(Project project, CancellationToken cancellationToken)
    {
        var createBuilderInvocation = await FindCreateBuilderInvocationAsync(project, cancellationToken);

        var variableDeclarator = createBuilderInvocation?.Ancestors()
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault();

        var builderVariable = variableDeclarator?.Identifier.Text;

        if (createBuilderInvocation != null)
        {
            var targetRoot = await createBuilderInvocation.SyntaxTree.GetRootAsync(cancellationToken);
            var document = project.GetDocument(createBuilderInvocation.SyntaxTree);

            if (createBuilderInvocation.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax builderIdentifier })
            {
                var addActorsStatement = SyntaxFactory.ParseStatement($"{builderVariable}.Services.AddActors(options => {{ }});");

                if (createBuilderInvocation.Ancestors().OfType<BlockSyntax>().FirstOrDefault() is SyntaxNode parentBlock)
                {
                    var firstChild = parentBlock.ChildNodes().FirstOrDefault(node => node is not UsingDirectiveSyntax);
                    var newParentBlock = parentBlock.InsertNodesAfter(firstChild, new[] { addActorsStatement });
                    targetRoot = targetRoot.ReplaceNode(parentBlock, newParentBlock);
                }
                else
                {                    
                    var compilationUnitSyntax = createBuilderInvocation.Ancestors().OfType<CompilationUnitSyntax>().FirstOrDefault();
                    var firstChild = compilationUnitSyntax.ChildNodes().FirstOrDefault(node => node is not UsingDirectiveSyntax);
                    var globalStatement = SyntaxFactory.GlobalStatement(addActorsStatement);
                    var newCompilationUnitSyntax = compilationUnitSyntax.InsertNodesAfter(firstChild, new[] { globalStatement });
                    targetRoot = targetRoot.ReplaceNode(compilationUnitSyntax, newCompilationUnitSyntax);
                }

                var addActorsInvocation = targetRoot?.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .FirstOrDefault(invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                        memberAccess.Name.Identifier.Text == "AddActors");

                return (document, addActorsInvocation);                
            }
        }

        return (null, null);
    }
}
