using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Dapr.Actors.Next.Analyzers;

/// <summary>
/// Provides practical fixes and scaffolds for Dapr Actors Next analyzer diagnostics.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ActorsNextCodeFixProvider))]
[Shared]
public sealed class ActorsNextCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the diagnostic ids this provider can fix.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(
        "DAPR1410",
        "DAPR1411",
        "DAPR1412",
        "DAPR1413",
        "DAPR1414",
        "DAPR1415",
        "DAPR1416",
        "DAPR1418");

    /// <summary>
    /// Gets the fix-all provider.
    /// </summary>
    public override FixAllProvider GetFixAllProvider() => null!;

    /// <summary>
    /// Registers available fixes for the diagnostic.
    /// </summary>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            switch (diagnostic.Id)
            {
                case "DAPR1410":
                    RegisterSolutionFix(context, diagnostic, "Promote current state baseline", PromoteBaselineAsync);
                    RegisterDocumentFix(context, diagnostic, "Scaffold actor state upcaster", ScaffoldStateUpcasterAsync);
                    break;
                case "DAPR1411":
                    RegisterDocumentFix(context, diagnostic, "Inline scheduled work", InlineScheduledWorkAsync);
                    break;
                case "DAPR1412":
                    RegisterDocumentFix(context, diagnostic, "Await instead of blocking", AwaitInsteadOfBlockingAsync);
                    break;
                case "DAPR1413":
                    RegisterDocumentFix(context, diagnostic, "Use TimeProvider", UseTimeProviderAsync);
                    break;
                case "DAPR1414":
                    RegisterDocumentFix(context, diagnostic, "Use seeded deterministic source", UseSeededSourceAsync);
                    break;
                case "DAPR1415":
                    RegisterDocumentFix(context, diagnostic, "Scaffold missing upcaster hop", ScaffoldMissingUpcasterAsync);
                    break;
                case "DAPR1416":
                    RegisterDocumentFix(context, diagnostic, "Replace filter logic with actor handoff", ReplaceFilterLogicAsync);
                    break;
                case "DAPR1418":
                    RegisterDocumentFix(context, diagnostic, "Bump actor contract version", BumpContractVersionAsync);
                    RegisterSolutionFix(context, diagnostic, "Promote current wire baseline", PromoteBaselineAsync);
                    break;
            }
        }

        return Task.CompletedTask;
    }

    private static void RegisterDocumentFix(
        CodeFixContext context,
        Diagnostic diagnostic,
        string title,
        Func<Document, Diagnostic, CancellationToken, Task<Document>> fix)
    {
        context.RegisterCodeFix(
            CodeAction.Create(title, token => fix(context.Document, diagnostic, token), title),
            diagnostic);
    }

    private static void RegisterSolutionFix(
        CodeFixContext context,
        Diagnostic diagnostic,
        string title,
        Func<Solution, Diagnostic, CancellationToken, Task<Solution>> fix)
    {
        context.RegisterCodeFix(
            CodeAction.Create(title, token => fix(context.Document.Project.Solution, diagnostic, token), title),
            diagnostic);
    }

    private static async Task<Document> InlineScheduledWorkAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var node = root.FindNode(diagnostic.Location.SourceSpan);
        if (node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Run" }, ArgumentList.Arguments.Count: > 0 } invocation)
        {
            var expression = UnwrapDelegate(invocation.ArgumentList.Arguments[0].Expression);
            return document.WithSyntaxRoot(root.ReplaceNode(invocation, expression.WithTriviaFrom(invocation)));
        }

        return document;
    }

    private static ExpressionSyntax UnwrapDelegate(ExpressionSyntax expression) =>
        expression switch
        {
            ParenthesizedLambdaExpressionSyntax { Body: ExpressionSyntax body } => body,
            SimpleLambdaExpressionSyntax { Body: ExpressionSyntax simpleBody } => simpleBody,
            AnonymousMethodExpressionSyntax { Body: BlockSyntax { Statements.Count: 1 } block } when
                block.Statements[0] is ReturnStatementSyntax { Expression: not null } returnStatement => returnStatement
                    .Expression,
            _ => expression
        };

    private static async Task<Document> AwaitInsteadOfBlockingAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        var node = root.FindNode(diagnostic.Location.SourceSpan);
        switch (node)
        {
            case MemberAccessExpressionSyntax { Name.Identifier.Text: "Result" } resultAccess:
            {
                var awaitExpression = SyntaxFactory.AwaitExpression(resultAccess.Expression.WithoutTrivia()).WithTriviaFrom(resultAccess);
                return document.WithSyntaxRoot(root.ReplaceNode(resultAccess, awaitExpression));
            }
            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Wait" } waitAccess } invocation:
            {
                var awaitExpression = SyntaxFactory.AwaitExpression(waitAccess.Expression.WithoutTrivia()).WithTriviaFrom(invocation);
                return document.WithSyntaxRoot(root.ReplaceNode(invocation, awaitExpression));
            }
            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "Thread" }, Name.Identifier.Text: "Sleep" } } sleepInvocation:
            {
                var delay = SyntaxFactory.ParseExpression("await Task.Delay(" + sleepInvocation.ArgumentList.Arguments.ToFullString() + ")");
                return document.WithSyntaxRoot(root.ReplaceNode(sleepInvocation, delay.WithTriviaFrom(sleepInvocation)));
            }
            default:
                return document;
        }
    }

    private static async Task<Document> UseTimeProviderAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        var node = root.FindNode(diagnostic.Location.SourceSpan);
        var replacement = node.ToString() switch
        {
            "DateTime.Now" => "TimeProvider.System.GetLocalNow().DateTime",
            "DateTime.UtcNow" => "TimeProvider.System.GetUtcNow().UtcDateTime",
            "DateTimeOffset.Now" => "TimeProvider.System.GetLocalNow()",
            "DateTimeOffset.UtcNow" => "TimeProvider.System.GetUtcNow()",
            "Stopwatch.StartNew()" => "TimeProvider.System.GetTimestamp()",
            _ => null,
        };

        if (replacement is null)
            return document;

        var expression = SyntaxFactory.ParseExpression(replacement).WithTriviaFrom(node);
        return document.WithSyntaxRoot(root.ReplaceNode(node, expression));
    }

    private static async Task<Document> UseSeededSourceAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        var node = root.FindNode(diagnostic.Location.SourceSpan);
        var replacement = node.ToString() switch
        {
            "new Random()" => "new Random(0)",
            "Random.Shared" => "new Random(0)",
            "Guid.NewGuid()" => "Guid.Empty",
            _ => null,
        };

        return replacement is null
            ? document
            : document.WithSyntaxRoot(root.ReplaceNode(node,
                SyntaxFactory.ParseExpression(replacement).WithTriviaFrom(node)));
    }

    private static async Task<Document> ScaffoldMissingUpcasterAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var fromType = diagnostic.Properties.TryGetValue("upcaster.from", out var from) ? from : "FromState";
        var toType = diagnostic.Properties.TryGetValue("upcaster.to", out var to) ? to : "ToState";
        return await AppendTextAsync(document, "\n" + $@"
public sealed class {SimpleName(fromType)}To{SimpleName(toType)}Upcaster : Dapr.Actors.Next.Abstractions.State.IActorStateUpcaster<{fromType}, {toType}>
{{
    public ValueTask<{toType}> UpcastAsync({fromType} state, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}}
").ConfigureAwait(false);
    }

    private static async Task<Document> ScaffoldStateUpcasterAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var stateName = diagnostic.Properties.TryGetValue("baseline.name", out var name) ? name : "ActorState";
        return await AppendTextAsync(document, "\n" + $@"
public sealed class {SimpleName(stateName)}Upcaster : Dapr.Actors.Next.Abstractions.State.IActorStateUpcaster<{stateName}, {stateName}>
{{
    public ValueTask<{stateName}> UpcastAsync({stateName} state, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}}
").ConfigureAwait(false);
    }

    private static async Task<Document> ReplaceFilterLogicAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var node = root.FindNode(diagnostic.Location.SourceSpan);
        var statement = node.AncestorsAndSelf().OfType<StatementSyntax>().FirstOrDefault();
        if (statement is null)
        {
            return document;
        }

        var newRoot = root.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
        return newRoot is null ? document : document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> BumpContractVersionAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var actorAttribute = root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .FirstOrDefault(static a => a.Name.ToString().EndsWith("DaprActor", StringComparison.Ordinal) ||
                                        a.Name.ToString().EndsWith("DaprActorAttribute", StringComparison.Ordinal));
        if (actorAttribute is null)
        {
            return document;
        }

        var argumentList = actorAttribute.ArgumentList ?? SyntaxFactory.AttributeArgumentList();
        var existingVersion = argumentList.Arguments.FirstOrDefault(static a => a.NameEquals?.Name.Identifier.Text == "ContractVersion");
        AttributeSyntax newAttribute;
        if (existingVersion is not null)
        {
            var current = existingVersion.Expression is LiteralExpressionSyntax literal && literal.Token.Value is int value ? value : 1;
            var newArgument = existingVersion.WithExpression(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(current + 1)));
            newAttribute = actorAttribute.WithArgumentList(argumentList.WithArguments(argumentList.Arguments.Replace(existingVersion, newArgument)));
        }
        else
        {
            var newArgument = SyntaxFactory.AttributeArgument(
                SyntaxFactory.NameEquals("ContractVersion"),
                nameColon: null,
                expression: SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(2)));
            var newArguments = argumentList.Arguments.Add(newArgument);
            newAttribute = actorAttribute.WithArgumentList(argumentList.WithArguments(newArguments));
        }

        return document.WithSyntaxRoot(root.ReplaceNode(actorAttribute, newAttribute));
    }

    private static async Task<Solution> PromoteBaselineAsync(Solution solution, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        if (!diagnostic.Properties.TryGetValue("baseline.current", out var currentLine) || string.IsNullOrWhiteSpace(currentLine) ||
            !diagnostic.Properties.TryGetValue("baseline.kind", out var kind) ||
            !diagnostic.Properties.TryGetValue("baseline.name", out var name))
        {
            return solution;
        }

        foreach (var project in solution.Projects)
        {
            foreach (var document in project.AdditionalDocuments)
            {
                if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileName(document.FilePath), ActorBaseline.ShippedFileName))
                {
                    continue;
                }

                var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
                var lines = text.ToString().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
                var prefix = kind + "|" + name + "|";
                var replaced = false;
                for (var i = 0; i < lines.Count; i++)
                {
                    if (lines[i].StartsWith(prefix, StringComparison.Ordinal))
                    {
                        lines[i] = currentLine;
                        replaced = true;
                    }
                }

                if (!replaced)
                {
                    lines.Add(currentLine);
                }

                return solution.WithAdditionalDocumentText(document.Id, SourceText.From(string.Join("\n", lines)));
            }
        }

        return solution;
    }

    private static async Task<Document> AppendTextAsync(Document document, string text)
    {
        var sourceText = await document.GetTextAsync().ConfigureAwait(false);
        var newText = sourceText.ToString() + text;
        return document.WithText(SourceText.From(newText));
    }

    private static string SimpleName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return "ActorState";
        }

        var index = fullName!.LastIndexOf('.');
        return index < 0 ? fullName : fullName.Substring(index + 1);
    }
}
